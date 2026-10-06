namespace DevPulse.Infrastructure.Identity;

public sealed class SeedAdminSettings
{
    public const string SectionName = "SeedAdmin";

    public string Email { get; set; } = "admin@devpulse.local";

    public string Password { get; set; } = "Admin123!";

    public string DisplayName { get; set; } = "Administrator";
}
