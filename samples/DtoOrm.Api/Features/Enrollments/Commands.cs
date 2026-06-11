using DtoOrm.Api.Application.Common;

namespace DtoOrm.Api.Features.Enrollments;

public sealed record EnrollStudentCommand(int StudentId, int OfferingId, DateOnly EnrolledAt) : ICommand<EnrollmentOutcome>;
public sealed record AssignGradeCommand(int EnrollmentId, string Grade) : ICommand<bool>;
public sealed record DropEnrollmentCommand(int EnrollmentId) : ICommand<bool>;
