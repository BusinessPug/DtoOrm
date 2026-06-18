using System.Security.Claims;

namespace DtoOrm.Api.Infrastructure.Auth;

public static class SchoolClaims
{
    public const string UserId = "school:user_id";
    public const string StudentId = "school:student_id";
    public const string TeacherId = "school:teacher_id";

    public static int? GetUserId(this ClaimsPrincipal user) => user.GetIntClaim(UserId);

    public static int? GetStudentId(this ClaimsPrincipal user) => user.GetIntClaim(StudentId);

    public static int? GetTeacherId(this ClaimsPrincipal user) => user.GetIntClaim(TeacherId);

    public static bool IsAdministrator(this ClaimsPrincipal user) => user.IsInRole(SchoolRoles.Administrator);

    public static bool IsTeacher(this ClaimsPrincipal user) => user.IsInRole(SchoolRoles.Teacher);

    public static bool IsStudent(this ClaimsPrincipal user) => user.IsInRole(SchoolRoles.Student);

    public static bool CanAccessStudent(this ClaimsPrincipal user, int studentId) =>
        user.IsAdministrator() || user.IsTeacher() || user.GetStudentId() == studentId;

    public static bool CanAccessTeacherWork(this ClaimsPrincipal user, int teacherId) =>
        user.IsAdministrator() || user.GetTeacherId() == teacherId;

    private static int? GetIntClaim(this ClaimsPrincipal user, string claimType)
    {
        var value = user.FindFirstValue(claimType);
        return int.TryParse(value, out var id) ? id : null;
    }
}
