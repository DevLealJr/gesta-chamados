namespace AlunosTech.Infrastructure.Security;

public class JwtOptions
{
    public string Key { get; set; } = "Changeme1234567890!Changeme1234567890!";
    public string Issuer { get; set; } = "AlunosTechHub";
    public string Audience { get; set; } = "AlunosTechHubMobile";
    public int ExpiryMinutes { get; set; } = 1440;
}
