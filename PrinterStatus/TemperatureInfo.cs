namespace print_bot;

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