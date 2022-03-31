using System.Collections.Concurrent;
using System.Diagnostics;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using print_bot;
using print_bot.PrinterStatus;

internal class Program
{
    private readonly string _serialPort;
    private readonly ulong _baudrate;
    private readonly ulong _guildId;
    private readonly ulong _channelId;
    private readonly ulong _startupMessageId;
    private readonly ulong _statusMessageId;

    private USBPrinter? _usbPrinter;

    private readonly BlockingCollection<Action> _eventQueue = new BlockingCollection<Action>();

    private readonly DiscordSocketClient _client = new DiscordSocketClient();


    private SocketGuild? _guild;
    private SocketTextChannel? _textChannel;

    private DateTime _lastUpdate = DateTime.Now;

    private Status _status = new Status();
    private bool _updateInProgress = false;

    private Program()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("config.json")
            .Build();

        _serialPort = configuration["serialport"] ?? throw new InvalidOperationException();
        _baudrate = ulong.Parse(configuration["baudrate"] ?? throw new InvalidOperationException());
        _guildId = ulong.Parse(configuration["guild"] ?? throw new InvalidOperationException());
        _channelId = ulong.Parse(configuration["channel"] ?? throw new InvalidOperationException());
        _startupMessageId = ulong.Parse(configuration["startup_message"] ?? throw new InvalidOperationException());
        _statusMessageId = ulong.Parse(configuration["status_message"] ?? throw new InvalidOperationException());

    }
    
    private static async Task Log(LogMessage logMessage)
    {
        await Console.Out.WriteLineAsync(logMessage.ToString());
    }

    private async Task AsyncMain(string[] args)
    {
        _client.Log += Log;
        _client.Ready += OnReady;
        _client.MessageReceived += OnMessage;
        
        await _client.LoginAsync(TokenType.Bot, Environment.GetEnvironmentVariable("TOKEN"));
        await _client.StartAsync();


        while (!_eventQueue.IsCompleted)
        {
            _eventQueue.Take()();
        }
    }

    private Task OnReady()
    {
        _usbPrinter = new USBPrinter(_serialPort, _baudrate, _eventQueue);
        _usbPrinter.OnStartupInfo += OnStartupInfo;
        _usbPrinter.OnPrintingStatus += OnPrintingStatus;
        _usbPrinter.OnTemperatureInfo += OnTemperatureInfo;
        
        _usbPrinter.PostGCode(new[]{
            "G28 X Y Z",
        });
        
        _guild = _client.GetGuild(_guildId);
        _textChannel = _guild.GetTextChannel(_channelId);
        
        return Task.CompletedTask;
    }

    private async Task OnMessage(SocketMessage message)
    {
        Debug.Assert(_textChannel != null, nameof(_textChannel) + " != null");
        Debug.Assert(_usbPrinter != null, nameof(_usbPrinter) + " != null");

        if (message.Channel.Id == _textChannel.Id)
        {
            var content = message.Content;
            if (content.StartsWith("!gcode"))
            {
                var code = content.Replace("!gcode", string.Empty).Trim();
                _usbPrinter.InterruptWithCode(code);
            }
            else if (content.StartsWith("!attachment"))
            {
                if (message.Attachments.Count == 1)
                {
                    var url = message.Attachments.First().Url;
                    using (var client = new HttpClient())
                    {
                        var str = await client.GetStringAsync(new Uri(url));
                        if (url.Last() == '/')
                        {
                            url = url.Substring(0, url.Length - 2);
                        }

                        _status.FileName = url.Split('/').Last();
                        _usbPrinter.PostGCode(str.Split("\n"));
                    }
                }
            }
            else if (content == "!pause")
            {
                _usbPrinter.Pause();
            }
            else if (content == "!resume")
            {
                _usbPrinter.Resume();
            }
            else if (content == "!abort")
            {
                _usbPrinter.Abort();
            }

            await message.DeleteAsync();
        }
    }

    private async void OnStartupInfo(StartupInfo startupInfo)
    {
        Debug.Assert(_textChannel != null, nameof(_textChannel) + " != null");
        
        await Console.Out.WriteLineAsync(startupInfo.ToString());
        await _textChannel.ModifyMessageAsync(_startupMessageId,
            properties => properties.Content = startupInfo.ToString());
    }

    private async void OnPrintingStatus(PrintingStatus printingStatus)
    {
        _status.PrintingStatus = printingStatus;
        await UpdateStatus();
        //await _client.SetStatusAsync(UserStatus.Online);
        await _client.SetGameAsync(_status.PrintingStatus.ToEmoji());
        //await _client.SetActivityAsync(new Game(printingStatus.ToEmoji(), ActivityType.CustomStatus, ActivityProperties.None, _status.ToString()));
    }

    private async void OnTemperatureInfo(TemperatureInfo temperatureInfo)
    {
        await Console.Out.WriteLineAsync(temperatureInfo.ToString());

        _status.TemperatureInfo = temperatureInfo;
        
        var nextPossibleUpdate = _lastUpdate + new TimeSpan(0, 0, 5);

        if (nextPossibleUpdate < DateTime.Now)
        {
            await UpdateStatus();
            _lastUpdate = DateTime.Now;
        }
    }

    private async Task UpdateStatus()
    {
        Debug.Assert(_textChannel != null, nameof(_textChannel) + " != null");
        
        await _textChannel.ModifyMessageAsync(_statusMessageId,
            prop => prop.Content = _status.ToString());
    }
    
    public static async Task Main(string[] args)
    {
        var program = new Program();
        await program.AsyncMain(args);
    }
}