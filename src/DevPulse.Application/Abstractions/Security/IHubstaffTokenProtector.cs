namespace DevPulse.Application.Abstractions.Security;

public interface IHubstaffTokenProtector
{
    string Protect(string plainText);

    string Unprotect(string protectedText);
}
