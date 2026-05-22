using DtoOrm.Api.Application.Common;

namespace DtoOrm.Api.Features.Enrollments;

public sealed record EnrollStudentCommand(int StudentId, int OfferingId, DateOnly EnrolledAt) : ICommand<int>;
public sealed record AssignGradeCommand(int EnrollmentId, string Grade) : ICommand<bool>;
public sealed record DropEnrollmentCommand(int EnrollmentId) : ICommand<bool>;
