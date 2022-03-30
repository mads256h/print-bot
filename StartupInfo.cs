namespace print_bot;

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