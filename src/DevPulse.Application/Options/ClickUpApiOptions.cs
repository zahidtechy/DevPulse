namespace DevPulse.Application.Options;

public sealed class ClickUpApiOptions
{
    public const string SectionName = "ClickUp";

    /// <summary>
    /// Minimum delay between ClickUp HTTP requests (helps stay under ~100 req/min).
    /// </summary>
    public int MinRequestIntervalMs { get; set; } = 750;

    /// <summary>
    /// How many times to retry after HTTP 429 before failing.
    /// </summary>
    public int MaxRetriesOnRateLimit { get; set; } = 8;

    /// <summary>
    /// Fallback wait when ClickUp does not send Retry-After.
    /// </summary>
    public int DefaultRetryAfterSeconds { get; set; } = 20;

    /// <summary>
    /// Max assignee IDs per task query (keeps URLs short and responses smaller).
    /// </summary>
    public int AssigneeBatchSize { get; set; } = 20;
}
