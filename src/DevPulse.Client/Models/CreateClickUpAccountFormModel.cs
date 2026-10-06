namespace DevPulse.Client.Models;

public class CreateClickUpAccountFormModel
{
    public string Name { get; set; } = string.Empty;
    public string WorkspaceId { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;

    public CreateClickUpAccountRequest ToRequest() =>
        new(Name.Trim(), WorkspaceId.Trim(), AccessToken.Trim());
}
