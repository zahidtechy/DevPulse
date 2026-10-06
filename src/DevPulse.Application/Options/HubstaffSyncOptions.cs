namespace DevPulse.Application.Options;

public sealed class HubstaffSyncOptions
{
    public const string SectionName = "HubstaffSync";

    public bool Enabled { get; set; } = true;

    public int LookbackDays { get; set; } = 90;

    public int IncrementalOverlapDays { get; set; } = 2;

    public int DateChunkDays { get; set; } = 31;

    public int RunHourUtc { get; set; } = 3;

    public int RunMinuteUtc { get; set; } = 0;

    public bool RunOnStartup { get; set; }
}
