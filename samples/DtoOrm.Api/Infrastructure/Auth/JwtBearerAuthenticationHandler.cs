using System.Net.Http.Headers;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace DtoOrm.Api.Infrastructure.Auth;

public sealed class JwtBearerAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly JwtTokenService _tokens;

    public JwtBearerAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        JwtTokenService tokens)
        : base(options, logger, encoder)
    {
        _tokens = tokens;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var headerValues))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (!AuthenticationHeaderValue.TryParse(headerValues.ToString(), out var header) ||
            !string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(header.Parameter))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid Authorization header."));
        }

        var principal = _tokens.ValidateToken(header.Parameter);
        if (principal is null)
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid or expired token."));
        }

        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
