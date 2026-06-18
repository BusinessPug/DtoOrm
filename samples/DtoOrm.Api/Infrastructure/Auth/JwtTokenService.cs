using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace DtoOrm.Api.Infrastructure.Auth;

public sealed class JwtTokenService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly JwtAuthOptions _options;
    private readonly TimeProvider _clock;

    public JwtTokenService(IOptions<JwtAuthOptions> options, TimeProvider clock)
    {
        _options = options.Value;
        _clock = clock;
    }

    public string CreateToken(AuthUser user)
    {
        var now = _clock.GetUtcNow();
        var expires = now.AddMinutes(_options.ExpirationMinutes);

        var header = new Dictionary<string, object>
        {
            ["alg"] = "HS256",
            ["typ"] = "JWT"
        };

        var payload = new Dictionary<string, object?>
        {
            ["iss"] = _options.Issuer,
            ["aud"] = _options.Audience,
            ["sub"] = user.Id.ToString(),
            ["preferred_username"] = user.Username,
            ["name"] = user.DisplayName,
            ["email"] = user.Email,
            ["role"] = user.Role,
            [SchoolClaims.UserId] = user.Id,
            [SchoolClaims.StudentId] = user.StudentId,
            [SchoolClaims.TeacherId] = user.TeacherId,
            ["iat"] = ToUnixSeconds(now),
            ["nbf"] = ToUnixSeconds(now),
            ["exp"] = ToUnixSeconds(expires)
        };

        var unsigned = $"{EncodeJson(header)}.{EncodeJson(payload)}";
        return $"{unsigned}.{Sign(unsigned, _options.SigningKey)}";
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            return null;
        }

        var unsigned = $"{parts[0]}.{parts[1]}";
        var expectedSignature = Sign(unsigned, _options.SigningKey);
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(expectedSignature),
                Encoding.ASCII.GetBytes(parts[2])))
        {
            return null;
        }

        using var document = JsonDocument.Parse(DecodeBase64Url(parts[1]));
        var payload = document.RootElement;
        if (!StringEquals(payload, "iss", _options.Issuer) || !StringEquals(payload, "aud", _options.Audience))
        {
            return null;
        }

        var now = ToUnixSeconds(_clock.GetUtcNow());
        if (!TryGetLong(payload, "exp", out var exp) || exp <= now)
        {
            return null;
        }

        if (TryGetLong(payload, "nbf", out var nbf) && nbf > now)
        {
            return null;
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, GetString(payload, "sub") ?? ""),
            new("preferred_username", GetString(payload, "preferred_username") ?? ""),
            new(ClaimTypes.Name, GetString(payload, "name") ?? ""),
            new(ClaimTypes.Email, GetString(payload, "email") ?? ""),
            new(ClaimTypes.Role, GetString(payload, "role") ?? "")
        };

        AddClaimIfPresent(claims, payload, SchoolClaims.UserId);
        AddClaimIfPresent(claims, payload, SchoolClaims.StudentId);
        AddClaimIfPresent(claims, payload, SchoolClaims.TeacherId);

        var identity = new ClaimsIdentity(claims, "Bearer", ClaimTypes.Name, ClaimTypes.Role);
        return new ClaimsPrincipal(identity);
    }

    private static void AddClaimIfPresent(List<Claim> claims, JsonElement payload, string claimType)
    {
        var value = GetString(payload, claimType);
        if (!string.IsNullOrWhiteSpace(value))
        {
            claims.Add(new Claim(claimType, value));
        }
    }

    private static bool StringEquals(JsonElement payload, string propertyName, string expected) =>
        string.Equals(GetString(payload, propertyName), expected, StringComparison.Ordinal);

    private static string? GetString(JsonElement payload, string propertyName)
    {
        if (!payload.TryGetProperty(propertyName, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }

    private static bool TryGetLong(JsonElement payload, string propertyName, out long value)
    {
        value = 0;
        return payload.TryGetProperty(propertyName, out var element) && element.TryGetInt64(out value);
    }

    private static long ToUnixSeconds(DateTimeOffset value) => value.ToUnixTimeSeconds();

    private static string EncodeJson<T>(T value) => EncodeBase64Url(JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions));

    private static string Sign(string unsignedToken, string signingKey)
    {
        var key = Encoding.UTF8.GetBytes(signingKey);
        using var hmac = new HMACSHA256(key);
        return EncodeBase64Url(hmac.ComputeHash(Encoding.ASCII.GetBytes(unsignedToken)));
    }

    private static string EncodeBase64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] DecodeBase64Url(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
        return Convert.FromBase64String(padded);
    }
}

public sealed record AuthUser(
    int Id,
    string Username,
    string Email,
    string PasswordHash,
    string Role,
    string DisplayName,
    int? StudentId,
    int? TeacherId,
    bool IsActive);
