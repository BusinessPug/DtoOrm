namespace DtoOrm.Api.Features.Enrollments;

public enum EnrollmentStatus
{
    Enrolled,
    AlreadyEnrolled,
    OfferingFull,
    OfferingNotFound
}

/// <summary>
/// The result of attempting to enroll a student. Produced inside a transaction that checks for a
/// duplicate enrollment and verifies the offering still has free seats before inserting.
/// </summary>
public sealed record EnrollmentOutcome(
    EnrollmentStatus Status,
    int? EnrollmentId = null,
    int? Capacity = null,
    long? Enrolled = null)
{
    public static EnrollmentOutcome Success(int enrollmentId) => new(EnrollmentStatus.Enrolled, enrollmentId);
    public static EnrollmentOutcome AlreadyEnrolled(int enrollmentId) => new(EnrollmentStatus.AlreadyEnrolled, enrollmentId);
    public static EnrollmentOutcome Full(int capacity, long enrolled) => new(EnrollmentStatus.OfferingFull, Capacity: capacity, Enrolled: enrolled);
    public static EnrollmentOutcome NotFound() => new(EnrollmentStatus.OfferingNotFound);
}

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
