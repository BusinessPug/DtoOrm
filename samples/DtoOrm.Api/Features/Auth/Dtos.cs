namespace DtoOrm.Api.Features.Auth;

public sealed record LoginRequest(string Username, string Password);

public sealed record LoginResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    CurrentUserDto User);

public sealed record CurrentUserDto(
    int Id,
    string Username,
    string Email,
    string DisplayName,
    string Role,
    int? StudentId,
    int? TeacherId);

public sealed record UserAccountDto(
    int Id,
    string Username,
    string Email,
    string DisplayName,
    string Role,
    int? StudentId,
    int? TeacherId,
    bool IsActive);
