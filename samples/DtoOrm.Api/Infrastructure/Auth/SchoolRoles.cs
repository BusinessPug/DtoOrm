namespace DtoOrm.Api.Infrastructure.Auth;

public static class SchoolRoles
{
    public const string Administrator = "Administrator";
    public const string Teacher = "Teacher";
    public const string Student = "Student";

    public const string AdminOrTeacher = Administrator + "," + Teacher;
    public const string AnyAuthenticatedRole = Administrator + "," + Teacher + "," + Student;
}
