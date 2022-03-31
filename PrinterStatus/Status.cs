using System.Text;

namespace print_bot.PrinterStatus;

public class Status
{
    public PrintingStatus PrintingStatus { get; set; } = PrintingStatus.Idling;
    public TemperatureInfo TemperatureInfo { get; set; } = new TemperatureInfo(string.Empty,string.Empty);

    public string FileName { get; set; } = string.Empty;

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine(PrintingStatus.ToEmoji() + " " +PrintingStatus.ToDisplayString());
        if (PrintingStatus != PrintingStatus.Idling)
        {
            sb.AppendLine($"File: {FileName}");
        }
        
        if (PrintingStatus == PrintingStatus.Heating)
        {
            sb.AppendLine(TemperatureInfo.ToString());
        }

        return sb.ToString();
    }
}