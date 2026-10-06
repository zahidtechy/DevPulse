namespace DevPulse.Application.Abstractions.Security;

public interface ITokenProtector
{
    string Protect(string plainText);

    string Unprotect(string protectedText);
}
