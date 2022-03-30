using print_bot;

internal class Program
{
    public static int Main(string[] args)
    {
        var printer = new USBPrinter("/dev/ttyACM0", 250000);
        printer.OnStartupInfo += (startupInfo) =>
        {
            Console.WriteLine(startupInfo);
        };
        
        Thread.Sleep(5000);
        printer.ReadStartupInfo();
        printer.PostGCode(new[]{
            "G28 X Y Z",
            "G1 F10000 X100 Y100",
            "G1 X0 Y100",
            "G1 X100 Y0",
            "G1 X0 Y0",
            "G1 X100 Y100",
            "G1 X0 Y100",
            "G1 X100 Y0",
            "G1 X0 Y0",
            "G1 X100 Y100",
            "G1 X0 Y100",
            "G1 X100 Y0",
            "G1 X0 Y0",
            "G1 X100 Y100",
            "G1 X0 Y100",
            "G1 X100 Y0",
            "G1 X0 Y0",
            "G1 X100 Y100",
            "G1 X0 Y100",
            "G1 X100 Y0",
            "G1 X0 Y0",
        });
        Thread.Sleep(1000);
        printer.Abort();
        Console.WriteLine("Pausing for 5 seconds");
        Thread.Sleep(5000);
        //printer.Resume();
        Console.WriteLine("Resuming");
        
        printer.InterruptWithCode("G1 X200 Y200");
        
        Thread.Sleep(1000);
        printer.InterruptWithCode("G1 X200 Y0");

        while (!printer.Events.IsCompleted)
        {
            printer.Events.Take()();
        }
        
        printer.Dispose();

        return 0;
    }
}