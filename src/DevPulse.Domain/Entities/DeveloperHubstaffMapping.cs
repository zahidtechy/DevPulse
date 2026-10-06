namespace DevPulse.Domain.Entities;

/// <summary>
/// Maps a DevPulse developer to a Hubstaff user within an organization.
/// </summary>
public class DeveloperHubstaffMapping
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DeveloperId { get; set; }

    public Developer Developer { get; set; } = null!;

    public Guid HubstaffOrganizationId { get; set; }

    public HubstaffOrganization HubstaffOrganization { get; set; } = null!;

    public int HubstaffUserId { get; set; }
}
