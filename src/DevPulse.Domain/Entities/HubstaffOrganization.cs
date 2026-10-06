namespace DevPulse.Domain.Entities;

/// <summary>
/// Hubstaff organization connection with encrypted PAT (refresh token) and sync metadata.
/// </summary>
public class HubstaffOrganization
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public int OrganizationId { get; set; }

    public string? HubstaffOrganizationName { get; set; }

    public string EncryptedPersonalAccessToken { get; set; } = string.Empty;

    public DateTime? PatExpiresAtUtc { get; set; }

    public DateOnly? LastSyncedToDate { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? LastValidatedAtUtc { get; set; }

    public string? LastValidationMessage { get; set; }

    public ICollection<DeveloperHubstaffMapping> Mappings { get; set; } = [];

    public ICollection<HubstaffDailyActivity> DailyActivities { get; set; } = [];
}
