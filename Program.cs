using System.Net;
using Discord;
using Discord.WebSocket;
using print_bot;

internal class Program
{
    private static async Task Log(LogMessage logMessage)
    {
        await Console.Out.WriteLineAsync(logMessage.ToString());
    }
    
    public static async Task Main(string[] args)
    {
        var client = new DiscordSocketClient();
        client.Log += Log;

        await client.LoginAsync(TokenType.Bot, Environment.GetEnvironmentVariable("TOKEN"));
        await client.StartAsync();
        
        var printer = new USBPrinter("/dev/ttyACM0", 250000);

        ITextChannel textChannel = null;
        
        client.Ready += async () =>
        {
            var guild = client.GetGuild(957955676376797184);
            textChannel = guild.GetTextChannel(958692640004640828);

            printer.OnStartupInfo += async (startupInfo) =>
            {
                Console.WriteLine(startupInfo);
                await textChannel.ModifyMessageAsync(958693485895118898,
                    properties => properties.Content = startupInfo.ToString());
            };

            PrintingStatus dprintingStatus = PrintingStatus.Idling;
            DateTime lastUpdate = DateTime.Now;

            printer.OnTemperatureInfo += async (temperatureInfo) =>
            {
                if (dprintingStatus == PrintingStatus.Heating && lastUpdate + new TimeSpan(0, 0, 5) < DateTime.Now)
                {
                    await textChannel.ModifyMessageAsync(958696125349625878,
                        properties =>
                            properties.Content = dprintingStatus.ToDisplayString() + "\n" + temperatureInfo.ToString());
                    lastUpdate = DateTime.Now;
                }

                Console.WriteLine(temperatureInfo);
            };

            printer.OnPrintingStatus += async (printingStatus) =>
            {
                dprintingStatus = printingStatus;
                await textChannel.ModifyMessageAsync(958696125349625878,
                    prop => prop.Content = printingStatus.ToDisplayString());
            };
        };

        client.MessageReceived += async message =>
        {
            if (message.Channel.Id == textChannel.Id)
            {
                var content = message.Content;
                if (content.StartsWith("!gcode"))
                {
                    var code = content.Replace("!gcode", string.Empty).Trim();
                    printer.InterruptWithCode(code);
                }
                else if (content.StartsWith("!attachment"))
                {
                    if (message.Attachments.Count == 1)
                    {
                        var url = message.Attachments.First().Url;
                        using (var client = new WebClient())
                        {
                            var str = await client.DownloadStringTaskAsync(new Uri(url));
                            printer.PostGCode(str.Split("\n"));
                        }
                    }
                }
                else if (content == "!pause")
                {
                    printer.Pause();
                }
                else if (content == "!resume")
                {
                    printer.Resume();
                }
                else if (content == "!abort")
                {
                    printer.Abort();
                }

                await message.DeleteAsync();
            }
        };

        
        Thread.Sleep(5000);
        printer.ReadStartupInfo();
        printer.PostGCode(new[]{
            "G28 X Y Z",
        });

        while (!printer.Events.IsCompleted)
        {
            printer.Events.Take()();
        }
        
        printer.Dispose();
    }
}