using System.Collections.Concurrent;
using System.Diagnostics;

namespace print_bot;

public sealed class USBPrinter : IDisposable
{
    private readonly BlockingCollection<Action<CancellationTokenSource>> _actions = new();
    private readonly ConcurrentQueue<string> _commandQueue = new ConcurrentQueue<string>();
    private readonly IDictionary<string, Action> _customCommandHandlers;

    private readonly Process _process;

    private CancellationTokenSource _currentCancellationSource = new CancellationTokenSource();

    private readonly Thread _runThread;

    private readonly object _printingStatusLock = new object();
    private PrintingStatus _printingStatus = PrintingStatus.Idling;

    private readonly BlockingCollection<Action> _eventQueue;

    public USBPrinter(string port, ulong baudrate, BlockingCollection<Action> eventQueue)
    {
        _eventQueue = eventQueue;

        var psi = new ProcessStartInfo("python3", $"serialport.py \"{port}\" {baudrate}")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        _process = Process.Start(psi) ?? throw new InvalidOperationException();

        _customCommandHandlers = new Dictionary<string, Action>
        {
            { "M105", GetTemperatureData },
            { "M109", WaitForTemperature },
            { "M190", WaitForTemperature }
        };
        
        _actions.Add(ReadStartupInfo);

        _runThread = new Thread(RunThread);
        _runThread.Start();
    }

    private void GetTemperatureData()
    {
        Debug.Assert(Thread.CurrentThread == _runThread, "Thread.CurrentThread == _runThread");

        if (!ReadLine().StartsWith("ok"))
        {
            throw new Exception();
        }
    }

    public event Action<StartupInfo>? OnStartupInfo;

    public event Action<TemperatureInfo>? OnTemperatureInfo;

    public event Action<PrintingStatus>? OnPrintingStatus;

    private void RunThread()
    {
        Debug.Assert(Thread.CurrentThread == _runThread, "Thread.CurrentThread == _runThread");
        
        while(!_actions.IsCompleted)
        {
            Action<CancellationTokenSource> action;
            try
            {
                action = _actions.Take();
            }
            catch (OperationCanceledException)
            {
                break;
            }
           
            ChangePrintingStatus(PrintingStatus.Printing);
            action(new CancellationTokenSource());

            if (_actions.Count == 0)
            {
                ChangePrintingStatus(PrintingStatus.Idling);
            }
        }
    }
    
    #region Action Queue Methods
    
    private void QueueConsumer(CancellationTokenSource token)
    {
        Debug.Assert(Thread.CurrentThread == _runThread, "Thread.CurrentThread == _runThread");
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
    

    private void Reset(CancellationTokenSource tokenSource)
    {
        Debug.Assert(Thread.CurrentThread == _runThread, "Thread.CurrentThread == _runThread");
        // Turn off extruder heater
        HandleCommand("M104 SO");
        
        // Turn off bed heater
        HandleCommand("M140 S0");
        
        // Go home
        HandleCommand("G28 X Y Z");
    }

    #endregion


    private void ChangePrintingStatus(PrintingStatus printingStatus)
    {
        lock (_printingStatusLock)
        {
            if (_printingStatus != printingStatus)
            {
                _printingStatus = printingStatus;
                _eventQueue.Add(() => OnPrintingStatus?.Invoke(printingStatus));
            }
        }
    }

    public void PostGCode(string[] gcode)
    {
        foreach (var command in gcode)
        {
            _commandQueue.Enqueue(command);
        }
        if (_actions.Count == 0)
        {
            _actions.Add(QueueConsumer);
        }
    }

    public void InterruptWithCode(string gcode)
    {
        Pause();
        _actions.Add((_) => HandleCommand(gcode));
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
        _actions.Add(QueueConsumer);
    }

    public void Abort()
    {
        lock (_currentCancellationSource)
        {
            _currentCancellationSource.Cancel();
        }
        _commandQueue.Clear();

        _actions.Add(Reset);
    }

    private string ReadLine()
    {
        Debug.Assert(Thread.CurrentThread == _runThread, "Thread.CurrentThread == _runThread");
        return _process.StandardOutput.ReadLine() ?? throw new InvalidOperationException();
    }

    private void WriteLine(string line)
    {
        Debug.Assert(Thread.CurrentThread == _runThread, "Thread.CurrentThread == _runThread");
        _process.StandardInput.WriteLine(line);
    }

    private void ReadStartupInfo(CancellationTokenSource token)
    {
        Debug.Assert(Thread.CurrentThread == _runThread, "Thread.CurrentThread == _runThread");
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

        var startupInfo = new StartupInfo(marlinVersion, lastUpdated, version, freeMemory, plannerBufferBytes,
            stepsPerUnit, maximumFeedRates, maximumAcceleration, acceleration, advancedVaraibles, homeOffset,
            pidSettings, sdCardStatus);

        _eventQueue.Add(() => OnStartupInfo?.Invoke(startupInfo));
    }

    private void HandleCommand(string command)
    {
        Debug.Assert(Thread.CurrentThread == _runThread, "Thread.CurrentThread == _runThread");
        
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
        Debug.Assert(Thread.CurrentThread == _runThread, "Thread.CurrentThread == _runThread");
        
        ChangePrintingStatus(PrintingStatus.Heating);
        string t;
        for (t = ReadLine(); t != "ok"; t = ReadLine())
        {
            Console.WriteLine(t);
            var splitTemps = t.Split(" ");
            var extruderTemp = splitTemps[0].Replace("T:", string.Empty);
            var bedTemp = splitTemps[2].Replace("B:", string.Empty);

            var temperatureInfo = new TemperatureInfo(extruderTemp, bedTemp);
            _eventQueue.Add(() => OnTemperatureInfo?.Invoke(temperatureInfo));
        }

        Console.WriteLine(t);
        ChangePrintingStatus(PrintingStatus.Printing);
    }

    public void Dispose()
    {
        _actions.CompleteAdding();
        _runThread.Join();
        _actions.Dispose();
        _process.Dispose();
        _eventQueue.CompleteAdding();
    }
}