using System.Security.Claims;
using DtoOrm.Api.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DtoOrm.Api.Features.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuth(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/login", async (
            [FromBody] LoginRequest request,
            AuthRepository users,
            PasswordHashVerifier passwords,
            JwtTokenService tokens,
            IOptions<JwtAuthOptions> options,
            TimeProvider clock,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                return Results.BadRequest(new { message = "Username and password are required." });
            }

            var user = await users.FindActiveUserAsync(request.Username.Trim(), ct);
            if (user is null || !passwords.Verify(request.Password, user.PasswordHash))
            {
                return Results.Unauthorized();
            }

            var token = tokens.CreateToken(user);
            var expires = clock.GetUtcNow().AddMinutes(options.Value.ExpirationMinutes);
            return Results.Ok(new LoginResponse(token, expires, ToDto(user)));
        })
        .AllowAnonymous();

        group.MapGet("/me", (ClaimsPrincipal principal) =>
        {
            var id = principal.GetUserId();
            if (id is null)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(new CurrentUserDto(
                id.Value,
                principal.FindFirstValue("preferred_username") ?? "",
                principal.FindFirstValue(ClaimTypes.Email) ?? "",
                principal.Identity?.Name ?? "",
                principal.FindFirstValue(ClaimTypes.Role) ?? "",
                principal.GetStudentId(),
                principal.GetTeacherId()));
        })
        .RequireAuthorization();

        group.MapGet("/users", async (AuthRepository users, CancellationToken ct) =>
            Results.Ok(await users.ListUsersAsync(ct)))
            .RequireAuthorization(policy => policy.RequireRole(SchoolRoles.Administrator));

        return app;
    }

    private static CurrentUserDto ToDto(AuthUser user) =>
        new(user.Id, user.Username, user.Email, user.DisplayName, user.Role, user.StudentId, user.TeacherId);
}
