namespace DevPulse.Infrastructure.Identity;

public sealed class RefreshToken
{
    public Guid Id { get; set; }

    public Guid FamilyId { get; set; }

    public Guid UserId { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public byte[] TokenHash { get; set; } = [];

    public string SecurityStamp { get; set; } = string.Empty;

    public string ClientType { get; set; } = string.Empty;

    public bool IsPersistent { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? ConsumedAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    public string? RevocationReason { get; set; }

    public Guid? ReplacedByTokenId { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
