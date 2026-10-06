namespace DevPulse.Application.Options;

public sealed class HubstaffApiOptions
{
    public const string SectionName = "Hubstaff";

    public int MinRequestIntervalMs { get; set; } = 500;

    public int MaxRetriesOnRateLimit { get; set; } = 5;

    public int DefaultPageLimit { get; set; } = 500;

    public int AccessTokenRefreshBufferSeconds { get; set; } = 60;

    public string AuthBaseUrl { get; set; } = "https://account.hubstaff.com/";

    public string ApiBaseUrl { get; set; } = "https://api.hubstaff.com/v2/";
}
