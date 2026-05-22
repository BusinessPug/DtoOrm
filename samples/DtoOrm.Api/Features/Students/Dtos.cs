namespace DtoOrm.Api.Features.Students;

public sealed record StudentDto(
    int Id,
    string FirstName,
    string LastName,
    string Email,
    DateOnly DateOfBirth,
    DateOnly EnrolledAt,
    bool IsActive);
