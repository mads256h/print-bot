using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reactive.Joins;

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

public class StartupInfo
{
    public StartupInfo(string marlinVersion, string lastUpdated, string version, string freeMemory, string plannerBufferBytes, string stepsPerUnit, string maximumFeedRates, string maximumAcceleration, string acceleration, string advancedVariables, string homeOffset, string pidSettings, string sdCardStatus)
    {
        MarlinVersion = marlinVersion;
        LastUpdated = lastUpdated;
        Version = version;
        FreeMemory = freeMemory;
        PlannerBufferBytes = plannerBufferBytes;
        StepsPerUnit = stepsPerUnit;
        MaximumFeedRates = maximumFeedRates;
        MaximumAcceleration = maximumAcceleration;
        Acceleration = acceleration;
        AdvancedVariables = advancedVariables;
        HomeOffset = homeOffset;
        PidSettings = pidSettings;
        SDCardStatus = sdCardStatus;
    }

    public string MarlinVersion { get; }
    public string LastUpdated { get; }
    public string Version { get; }
    public string FreeMemory { get; }
    public string PlannerBufferBytes { get; }
    public string StepsPerUnit { get; }
    public string MaximumFeedRates { get; }
    public string MaximumAcceleration { get; }
    public string Acceleration { get; }
    public string AdvancedVariables { get; }
    public string HomeOffset { get; }
    public string PidSettings { get; }
    public string SDCardStatus { get; }

    public override string ToString()
    {
        return
            $"MarlinVersion: {MarlinVersion}\n" +
            $"LastUpdated: {LastUpdated}\n" +
            $"Version: {Version}\n" +
            $"FreeMemory: {FreeMemory}\n" +
            $"PlannerBufferBytes: {PlannerBufferBytes}\n" +
            $"StepsPerUnit: {StepsPerUnit}\n" +
            $"MaximumFeedRates: {MaximumFeedRates}\n" +
            $"MaximumAcceleration: {MaximumAcceleration}\n" +
            $"Acceleration: {Acceleration}\n" +
            $"AdvancedVariables: {AdvancedVariables}\n" +
            $"HomeOffset: {HomeOffset}\n" +
            $"PidSettings: {PidSettings}\n" +
            $"SDCardStatus: {SDCardStatus}";
    }
}

public sealed class USBPrinter : IDisposable
{
    private readonly BlockingCollection<Action<CancellationTokenSource>> _actions = new();
    private readonly ConcurrentQueue<string> _commandQueue = new ConcurrentQueue<string>();
    private readonly IDictionary<string, Action> _customCommandHandlers;

    private readonly Process _process;

    private CancellationTokenSource _currentCancellationSource = new CancellationTokenSource();

    private readonly Thread _runThread;

    public USBPrinter(string port, int baudrate)
    {
        var psi = new ProcessStartInfo("python", $"serialport.py \"{port}\" {baudrate}")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        _process = Process.Start(psi) ?? throw new InvalidOperationException();

        _customCommandHandlers = new Dictionary<string, Action>
        {
            //{ "M105", GetTemperatureData },
            { "M109", WaitForTemperature },
            { "M190", WaitForTemperature }
        };

        _runThread = new Thread(RunThread);
        _runThread.Start();
    }

    public BlockingCollection<Action> Events { get; } = new BlockingCollection<Action>();

    public event Action<StartupInfo> OnStartupInfo;

    private void RunThread()
    {
        bool isCompleted = false;
        lock (_actions)
        {
            isCompleted = _actions.IsCompleted;
        }
        while(!isCompleted)
        {
            var action = _actions.Take();
            
            action(new CancellationTokenSource());
            
            lock (_actions)
            {
                isCompleted = _actions.IsCompleted;
            }
        }
    }

    private void QueueConsumer(CancellationTokenSource token)
    {
        lock (_currentCancellationSource)
        {
            _currentCancellationSource = token;
        }
        while (!token.IsCancellationRequested && _commandQueue.TryDequeue(out var command))
        {
            HandleCommand(command);
        }
        /*
        lock (_currentCancellationSource)
        {
            _currentCancellationSource.Dispose();
        }
        */
    }

    public void PostGCode(string[] gcode)
    {
        foreach (var command in gcode)
        {
            _commandQueue.Enqueue(command);
        }
        lock (_actions)
        {
            if (_actions.Count == 0)
            {
                _actions.Add(QueueConsumer);
            }
        }
    }

    public void InterruptWithCode(string gcode)
    {
        Pause();
        lock (_actions)
        {
            _actions.Add((token) => HandleCommand(gcode));
        }
        Resume();
    }

    public void Pause()
    {
        lock (_currentCancellationSource)
        {
            _currentCancellationSource.Cancel();
        }
    }

    public void Resume()
    {
        lock (_actions)
        {
            _actions.Add(QueueConsumer);
        }
    }

    public void Abort()
    {
        lock (_currentCancellationSource)
        {
            _currentCancellationSource.Cancel();
        }
        _commandQueue.Clear();
    }

    private string ReadLine()
    {
        return _process.StandardOutput.ReadLine() ?? throw new InvalidOperationException();
    }

    private void WriteLine(string line)
    {
        _process.StandardInput.WriteLine(line);
    }

    public void ReadStartupInfo()
    {
        /*
        start
        echo:Marlin 1.0.0
        echo: Last Updated: Mar 15 2018 13:04:08 | Author: Vers:_3.3.0
        Compiled: Mar 15 2018
        echo: Free Memory: 2055  PlannerBufferBytes: 1232
        echo:Stored settings retrieved
        echo:Steps per unit:
        echo:  M92 X80.00 Y80.00 Z200.00 E369.00
        echo:Maximum feedrates (mm/s):
        echo:  M203 X300.00 Y300.00 Z40.00 E45.00
        echo:Maximum Acceleration (mm/s2):
        echo:  M201 X9000 Y9000 Z100 E10000
        echo:Acceleration: S=acceleration, T=retract acceleration
        echo:  M204 S5000.00 T3000.00
        echo:Advanced variables: S=Min feedrate (mm/s), T=Min travel feedrate (mm/s), B=minimum segment time (ms), X=maximum XY jerk (mm/s),  Z=maximum Z jerk (mm/s),  E=maximum E jerk (mm/s)
        echo:  M205 S0.00 T0.00 B20000 X30.00 Z0.40 E5.00
        echo:Home offset (mm):
        echo:  M206 X0.00 Y0.00 Z-14.65
        echo:PID settings:
        echo:   M301 P10.03 I1.50 D70.00
        echo:SD card ok
        */

        string RemoveEcho()
        {
            return
                ReadLine().Replace("echo:", string.Empty)
                    .Trim();
        }
        
        if (ReadLine() != "start") throw new Exception();
        var marlinVersion = RemoveEcho();
        var versionLine = RemoveEcho();
        var lastUpdated = versionLine.Split('|')[0]
            .Replace("Last Updated:", string.Empty)
            .Trim();
        var version = versionLine.Split("|")[1]
            .Replace("Author: Vers:_", string.Empty)
            .Trim();
        var compiled = ReadLine();
        var freeMemoryLine = RemoveEcho();
        var freeMemory = freeMemoryLine.Split("  ")[0]
            .Replace("Free Memory:", string.Empty)
            .Trim();
        var plannerBufferBytes = freeMemoryLine.Split("  ")[1]
            .Replace("PlannerBufferBytes:", string.Empty)
            .Trim();
        ReadLine();
        
        ReadLine();
        var stepsPerUnit = RemoveEcho();
        
        ReadLine();
        var maximumFeedRates = RemoveEcho();

        ReadLine();
        var maximumAcceleration = RemoveEcho();

        ReadLine();
        var acceleration = RemoveEcho();

        ReadLine();
        var advancedVaraibles = RemoveEcho();

        ReadLine();
        var homeOffset = RemoveEcho();

        ReadLine();
        var pidSettings = RemoveEcho();

        var sdCardStatus = RemoveEcho();

        var startupInfo = new StartupInfo(marlinVersion, lastUpdated, version, freeMemory, plannerBufferBytes, stepsPerUnit, maximumFeedRates, maximumAcceleration, acceleration, advancedVaraibles, homeOffset, pidSettings, sdCardStatus);
        
        Events.Add(() => OnStartupInfo.Invoke(startupInfo));
    }

    private void HandleCommand(string command)
    {
        Console.WriteLine(command);

        command = command.Split(";")[0];
        command = command.Trim();
        if (command == string.Empty)
            // Ignore comments and whitespace
            return;

        WriteLine(command);

        var firstPart = command.Split(" ")[0];
        if (_customCommandHandlers.TryGetValue(firstPart, out var handler))
        {
            handler();
        }
        else
        {
            // Check that the command returned "ok";
            var response = ReadLine();
            Console.WriteLine(response);
            if (response != "ok") throw new Exception();
        }
    }
    
    private void WaitForTemperature()
    {
        string t;
        for (t = ReadLine(); t != "ok"; t = ReadLine())
        {
            Console.WriteLine(t);
        }

        Console.WriteLine(t);
    }

    public void Dispose()
    {
        _actions.CompleteAdding();
        _runThread.Join();
        _actions.Dispose();
        _process.Dispose();
    }
}