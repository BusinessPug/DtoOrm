namespace DtoOrm.Api.Infrastructure.Auth;

public sealed class JwtAuthOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "DtoOrm.School";
    public string Audience { get; set; } = "DtoOrm.School.Portal";
    public string SigningKey { get; set; } = "";
    public int ExpirationMinutes { get; set; } = 120;
}
