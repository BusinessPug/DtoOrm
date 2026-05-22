using DtoOrm.Api.Application.Common;

namespace DtoOrm.Api.Features.Courses;

public sealed record CreateCourseCommand(string Code, string Title, int Credits, int DepartmentId) : ICommand<int>;
public sealed record UpdateCourseCommand(int Id, string Code, string Title, int Credits, int DepartmentId) : ICommand<bool>;
public sealed record DeleteCourseCommand(int Id) : ICommand<bool>;
