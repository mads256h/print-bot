using System.Collections.Concurrent;
using System.IO.Ports;
using System.Text;

namespace print_bot;

public enum PrintingStatus
{
    Idling,
    Heating,
    Printing,
    Paused
}

public class PrinterInfo
{
    public TemperatureInfo TemperatureInfo { get; }

    public PrinterInfo(TemperatureInfo temperatureInfo)
    {
        TemperatureInfo = temperatureInfo;
    }

    public override string ToString()
    {
        return TemperatureInfo.ToString();
    }
}

public class TemperatureInfo
{
    public string ExtruderTemp { get; }
    public string BedTemp { get; }

    public TemperatureInfo(string extruderTemp, string bedTemp)
    {
        ExtruderTemp = extruderTemp;
        BedTemp = bedTemp;
    }

    public override string ToString()
    {
        return $"ExtruderTemp: {ExtruderTemp}, BedTemp: {BedTemp}";
    }
}

public static class PrintingStatusExtensions
{
    public static string ToDisplayString(this PrintingStatus printingStatus)
    {
        switch (printingStatus)
        {
            case PrintingStatus.Idling:
                return "Printer is idling";
            case PrintingStatus.Heating:
                return "Printer is heating";
            case PrintingStatus.Printing:
                return "Printer is printing";
            case PrintingStatus.Paused:
                return "Printer is paused";
            default:
                throw new ArgumentOutOfRangeException(nameof(printingStatus), printingStatus, null);
        }
    }
}

public sealed class USBPrinter : IDisposable
{
    private readonly IReadOnlyDictionary<string, Action> _customCommandHandlers;
    private readonly Mutex _pauseMutex = new();

    private readonly SerialPort _port;
    private readonly object _statusSyncronizer = new();
    private PrintingStatus _beforePauseStatus = PrintingStatus.Idling;
    private readonly ConcurrentQueue<string> _commandQueue = new();
    private PrintingStatus _printingStatus = PrintingStatus.Idling;

    private bool _shouldExit = false;

    public USBPrinter(string port, int baudrate)
    {
        _port = new SerialPort(port, baudrate);
        _port.Encoding = Encoding.ASCII;
        _port.NewLine = "\n";
        _port.ErrorReceived += PortOnErrorReceived;
        _port.Open();

        _customCommandHandlers = new Dictionary<string, Action>
        {
            { "M105", GetTemperatureData },
            { "M109", WaitForTemperature },
            { "M190", WaitForTemperature }
        };
    }
    
    public delegate void TemperatureChangeHandler(TemperatureInfo info);

    public event TemperatureChangeHandler OnTemperatureChange;

    public void Exit()
    {
        _commandQueue.Clear();
        bool shouldResume = false;
        lock (_statusSyncronizer)
        {
            shouldResume = _printingStatus == PrintingStatus.Paused;
        }

        if (shouldResume)
        {
            Resume();
        }
        _shouldExit = true;
    }

    public void Dispose()
    {
        _port.Dispose();
    }

    public Task Run()
    {
        return Task.Run(() =>
        {
            while (!_shouldExit)
            {
                while (_commandQueue.TryDequeue(out var command))
                {
                    _pauseMutex.WaitOne();
                    HandleCommand(command);
                    _pauseMutex.ReleaseMutex();
                }

                lock (_statusSyncronizer)
                {
                    _printingStatus = PrintingStatus.Idling;
                }

                Thread.Sleep(1000);
            }
        });
    }

    public void SendGCode(params string[] gcode)
    {
        foreach (var s in gcode) _commandQueue.Enqueue(s);
    }

    public void Pause()
    {
        lock (_statusSyncronizer)
        {
            _beforePauseStatus = _printingStatus;
            _printingStatus = PrintingStatus.Paused;
        }

        Console.WriteLine("Pausing");
        _pauseMutex.WaitOne();
    }

    public void Resume()
    {
        lock (_statusSyncronizer)
        {
            _printingStatus = _beforePauseStatus;
        }

        Console.WriteLine("Resuming");
        _pauseMutex.ReleaseMutex();
    }

    public void Abort()
    {
        Console.WriteLine("Abort");
        _commandQueue.Clear();

        // Go to home position
        _commandQueue.Enqueue("G28 X Y Z");

        // Turn off extruder heater
        _commandQueue.Enqueue("M104 S0");

        // Turn off bed heater
        _commandQueue.Enqueue("M140 S0");
    }

    public PrintingStatus GetPrintingStatus()
    {
        lock (_statusSyncronizer)
        {
            return _printingStatus;
        }
    }

    private void HandleCommand(string command)
    {
        lock (_statusSyncronizer)
        {
            _printingStatus = PrintingStatus.Printing;
        }

        Console.WriteLine(command);

        command = command.Split(";")[0];
        command = command.Trim();
        if (command == string.Empty)
            // Ignore comments and whitespace
            return;

        _port.WriteLine(command);

        var firstPart = command.Split(" ")[0];
        if (_customCommandHandlers.TryGetValue(firstPart, out var handler))
        {
            handler();
        }
        else
        {
            // Check that the command returned "ok";
            var response = _port.ReadLine();
            Console.WriteLine(response);
            if (response != "ok") throw new Exception();
        }
    }

    public PrinterInfo GetPrinterInfo()
    {
        TemperatureInfo tempInfo = new TemperatureInfo("Invalid", "Invalid");
        Pause();
        var t = new TemperatureChangeHandler((info) => tempInfo = info);
        OnTemperatureChange += t; 
        HandleCommand("M115");
        HandleCommand("M105");
        OnTemperatureChange -= t;
        Resume();

        return new PrinterInfo(tempInfo);
    }

    private void PortOnErrorReceived(object sender, SerialErrorReceivedEventArgs e)
    {
        throw new NotImplementedException();
    }

    private void GetTemperatureData()
    {
        string t = _port.ReadLine();
        Console.WriteLine(t);
        var split = t.Split(" ");
        var status = split[0];
        if (status != "ok")
        {
            throw new Exception();
        }

        if (split.Length == 3)
        {
            var extruderTemp = split[1].Replace("T:", string.Empty);
            var bedTemp = split[2].Replace("B:", string.Empty);
            OnTemperatureChange?.Invoke(new TemperatureInfo(extruderTemp, bedTemp));
        }
    }
    
    private void WaitForTemperature()
    {
        lock (_statusSyncronizer)
        {
            _printingStatus = PrintingStatus.Heating;
        }

        string t;
        for (t = _port.ReadLine(); t != "ok"; t = _port.ReadLine())
        {
            Console.WriteLine(t);
        }

        lock (_statusSyncronizer)
        {
            _printingStatus = PrintingStatus.Printing;
        }

        Console.WriteLine(t);
    }
}