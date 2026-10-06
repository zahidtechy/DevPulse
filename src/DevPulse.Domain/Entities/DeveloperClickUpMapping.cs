namespace DevPulse.Domain.Entities;

/// <summary>
/// Maps a DevPulse developer to a ClickUp user ID within a specific workspace account.
/// </summary>
public class DeveloperClickUpMapping
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DeveloperId { get; set; }

    public Developer Developer { get; set; } = null!;

    public Guid ClickUpAccountId { get; set; }

    public ClickUpAccount ClickUpAccount { get; set; } = null!;

    public int ClickUpUserId { get; set; }
}
