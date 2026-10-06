using DevPulse.Domain.Entities;

namespace DevPulse.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Optional link to a <see cref="Developer"/> record for leave, attendance, and related features.
    /// When set, this takes precedence over email-based developer matching.
    /// </summary>
    public Guid? DeveloperId { get; set; }

    public Developer? Developer { get; set; }
}
