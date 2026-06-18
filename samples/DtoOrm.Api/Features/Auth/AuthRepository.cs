using DtoOrm.Api.Generated;
using DtoOrm.Api.Infrastructure.Auth;
using DtoOrm.Core;

namespace DtoOrm.Api.Features.Auth;

public sealed class AuthRepository
{
    private readonly OrmSession _session;

    public AuthRepository(OrmSession session) => _session = session;

    public Task<AuthUser?> FindActiveUserAsync(string login, CancellationToken cancellationToken)
    {
        var u = Db.Tables.AppUsers;
        return _session
            .From(u)
            .Select(
                u.Id,
                u.Username,
                u.Email,
                u.PasswordHash,
                u.Role,
                u.DisplayName,
                u.StudentId,
                u.TeacherId,
                u.IsActive)
            .Where((u.Username.Eq(login) | u.Email.Eq(login)) & u.IsActive.Eq(true))
            .SingleOrDefaultAsync<AuthUser>(cancellationToken);
    }

    public Task<IReadOnlyList<UserAccountDto>> ListUsersAsync(CancellationToken cancellationToken)
    {
        var u = Db.Tables.AppUsers;
        return _session
            .From(u)
            .Select(u.Id, u.Username, u.Email, u.DisplayName, u.Role, u.StudentId, u.TeacherId, u.IsActive)
            .OrderBy(u.Role)
            .OrderBy(u.DisplayName)
            .ToListAsync<UserAccountDto>(cancellationToken);
    }
}
