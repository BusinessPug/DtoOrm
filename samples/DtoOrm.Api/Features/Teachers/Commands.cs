using DtoOrm.Api.Application.Common;

namespace DtoOrm.Api.Features.Teachers;

public sealed record CreateTeacherCommand(
    string FirstName,
    string LastName,
    string Email,
    int DepartmentId,
    DateOnly HiredAt) : ICommand<int>;

public sealed record UpdateTeacherCommand(
    int Id,
    string FirstName,
    string LastName,
    string Email,
    int DepartmentId,
    DateOnly HiredAt,
    bool IsActive) : ICommand<bool>;

public sealed record DeleteTeacherCommand(int Id) : ICommand<bool>;
