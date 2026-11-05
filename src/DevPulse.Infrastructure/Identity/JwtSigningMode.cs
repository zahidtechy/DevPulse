namespace DevPulse.Infrastructure.Identity;

public enum JwtSigningMode
{
    Auto = 0,
    Certificate = 1,
    Symmetric = 2,
    DevelopmentEphemeral = 3
}
