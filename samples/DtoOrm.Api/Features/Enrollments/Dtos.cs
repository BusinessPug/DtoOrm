namespace DtoOrm.Api.Features.Enrollments;

public sealed record EnrollmentDto(
    int Id,
    int StudentId,
    int OfferingId,
    DateOnly EnrolledAt,
    string? Grade);

public sealed record TranscriptEntryDto(
    int EnrollmentId,
    int OfferingId,
    int CourseId,
    string CourseCode,
    string CourseTitle,
    int Credits,
    int TermId,
    string TermCode,
    string TermName,
    int TeacherId,
    string TeacherFirstName,
    string TeacherLastName,
    DateOnly EnrolledAt,
    string? Grade);
