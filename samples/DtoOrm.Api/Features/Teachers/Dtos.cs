namespace DtoOrm.Api.Features.Teachers;

public sealed record TeacherDto(
    int Id,
    string FirstName,
    string LastName,
    string Email,
    int DepartmentId,
    DateOnly HiredAt,
    bool IsActive);
