namespace DtoOrm.Api.Features.Courses;

public sealed record CourseDto(int Id, string Code, string Title, int Credits, int DepartmentId);
