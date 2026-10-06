using Microsoft.AspNetCore.DataProtection;

namespace DevPulse.Infrastructure.Security;

public sealed class HubstaffTokenProtector : IHubstaffTokenProtector
{
    private readonly IDataProtector _protector;

    public HubstaffTokenProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("DevPulse.Hubstaff.PersonalAccessTokens.v1");
    }

    public string Protect(string plainText) => _protector.Protect(plainText);

    public string Unprotect(string protectedText) => _protector.Unprotect(protectedText);
}
