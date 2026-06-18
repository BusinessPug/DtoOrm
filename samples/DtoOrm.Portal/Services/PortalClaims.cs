using System.Security.Claims;

namespace DtoOrm.Portal.Services;

public static class PortalClaims
{
    public const string AccessToken = "access_token";
    public const string Username = "preferred_username";
    public const string StudentId = "school:student_id";
    public const string TeacherId = "school:teacher_id";

    public static int? GetStudentId(this ClaimsPrincipal user) => user.GetIntClaim(StudentId);

    public static int? GetTeacherId(this ClaimsPrincipal user) => user.GetIntClaim(TeacherId);

    private static int? GetIntClaim(this ClaimsPrincipal user, string claimType)
    {
        var value = user.FindFirstValue(claimType);
        return int.TryParse(value, out var id) ? id : null;
    }
}
