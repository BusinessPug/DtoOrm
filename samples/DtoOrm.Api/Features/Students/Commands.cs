using DtoOrm.Api.Application.Common;

namespace DtoOrm.Api.Features.Students;

public sealed record CreateStudentCommand(
    string FirstName,
    string LastName,
    string Email,
    DateOnly DateOfBirth,
    DateOnly EnrolledAt) : ICommand<int>;

public sealed record UpdateStudentCommand(
    int Id,
    string FirstName,
    string LastName,
    string Email,
    DateOnly DateOfBirth,
    DateOnly EnrolledAt,
    bool IsActive) : ICommand<bool>;

public sealed record DeleteStudentCommand(int Id) : ICommand<bool>;
