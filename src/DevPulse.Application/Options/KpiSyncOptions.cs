namespace DevPulse.Application.Options;

public sealed class KpiSyncOptions
{
    public const string SectionName = "KpiSync";

    /// <summary>
    /// When false, the hosted daily job does not run (manual sync still works).
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// How many days of ClickUp task history to pull on each sync.
    /// </summary>
    public int LookbackDays { get; set; } = 90;

    /// <summary>
    /// Hour of day (UTC) when the daily sync should start.
    /// </summary>
    public int RunHourUtc { get; set; } = 2;

    /// <summary>
    /// Minute of hour (UTC) when the daily sync should start.
    /// </summary>
    public int RunMinuteUtc { get; set; } = 0;

    /// <summary>
    /// When true, run one sync shortly after the server starts.
    /// </summary>
    public bool RunOnStartup { get; set; }
}
