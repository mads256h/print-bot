using print_bot;

internal class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("Hello, World!");
        var printer = new USBPrinter("/dev/ttyACM0", 9600);

        Thread.Sleep(2000);

        var t = printer.Run();
        Console.WriteLine(printer.GetPrintingStatus());

        // Reset to home position
        // This also enables movement
        printer.SendGCode("G28 X Y Z");
        Console.WriteLine(printer.GetPrintingStatus());

        var gcode = new[]
        {
            "G1 X2 Y2",
            "; Comment",
            "M80 ; End of line comment",
            "M190 S60",
            "M191",
            "M192",
            "M193",
            "M194",
            "M195"
        };

        printer.SendGCode(gcode);
        Console.WriteLine(printer.GetPrintingStatus().ToDisplayString());

        Thread.Sleep(1000);
        printer.Pause();
        Console.WriteLine(printer.GetPrintingStatus().ToDisplayString());

        Thread.Sleep(2000);
        printer.Resume();
        Console.WriteLine(printer.GetPrintingStatus().ToDisplayString());

        Thread.Sleep(3000);
        printer.Abort();
        Console.WriteLine(printer.GetPrintingStatus().ToDisplayString());

        printer.SendGCode("M109");
        Console.WriteLine(printer.GetPrintingStatus().ToDisplayString());

        printer.Exit();

        await t;

        printer.Dispose();
    }
}