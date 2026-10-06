namespace DevPulse.Shared.Constants;

public static class AppBranding
{
    public const string CompanyName = "Datavanched";

    public const string ProductTagline = "Developer Work Platform";

    public const string NavSubtitle = "ClickUp Analytics";

    public static string PageTitle(string? page = null) =>
        page is null ? CompanyName : $"{page} - {CompanyName}";
}
