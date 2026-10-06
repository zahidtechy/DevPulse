namespace DevPulse.Shared.Constants;

/// <summary>
/// Fixed identifiers for seeded demo ClickUp accounts, developers, and workspace data.
/// </summary>
public static class DemoSeedData
{
    public const string WorkspaceIdPrefix = "SEED-";
    public const string DemoAccessToken = "demo-seed-token";

    public static readonly Guid InternalAccountId = Guid.Parse("11111111-1111-1111-1111-111111111101");
    public static readonly Guid AcmeAccountId = Guid.Parse("11111111-1111-1111-1111-111111111102");

    public static readonly Guid SarahChenId = Guid.Parse("22222222-2222-2222-2222-222222222201");
    public static readonly Guid JamesOkonkwoId = Guid.Parse("22222222-2222-2222-2222-222222222202");
    public static readonly Guid PriyaSharmaId = Guid.Parse("22222222-2222-2222-2222-222222222203");
    public static readonly Guid MarcusWebbId = Guid.Parse("22222222-2222-2222-2222-222222222204");

    public static bool IsDemoWorkspace(string workspaceId) =>
        workspaceId.StartsWith(WorkspaceIdPrefix, StringComparison.Ordinal);
}
