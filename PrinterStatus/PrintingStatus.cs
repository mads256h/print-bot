namespace print_bot;

public enum PrintingStatus
{
    Idling,
    Heating,
    Printing,
    Paused
}

public static class PrintingStatusExtensions
{
    public static string ToEmoji(this PrintingStatus printingStatus)
    {
        switch (printingStatus)
        {
            case PrintingStatus.Idling:
                return "💤";
            case PrintingStatus.Heating:
                return "🔥";
            case PrintingStatus.Printing:
                return "🖨️";
            case PrintingStatus.Paused:
                return "⏸";
            default:
                throw new ArgumentOutOfRangeException(nameof(printingStatus), printingStatus, null);
        }
    }
    
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
