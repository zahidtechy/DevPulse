namespace DevPulse.Client.Models;

public class CreateHubstaffOrganizationFormModel
{
    public string Name { get; set; } = string.Empty;

    public string PersonalAccessToken { get; set; } = string.Empty;

    public string? SelectedOrganizationId { get; set; }

    public CreateHubstaffOrganizationRequest ToRequest()
    {
        int? orgId = int.TryParse(SelectedOrganizationId, out var parsed) ? parsed : null;
        return new CreateHubstaffOrganizationRequest(Name.Trim(), PersonalAccessToken.Trim(), orgId);
    }
}
