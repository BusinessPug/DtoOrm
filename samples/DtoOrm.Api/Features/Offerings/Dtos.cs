namespace DtoOrm.Api.Features.Offerings;

public sealed record OfferingDto(
    int Id,
    int CourseId,
    int TeacherId,
    int TermId,
    int Capacity,
    string Room);

/// <summary>
/// A fully resolved offering with related course, teacher and term names. Backed by a query that
/// joins offerings to courses, teachers and terms.
/// </summary>
public sealed record OfferingDetailsDto(
    int Id,
    int CourseId,
    string CourseCode,
    string CourseTitle,
    int Credits,
    int TeacherId,
    string TeacherFirstName,
    string TeacherLastName,
    int TermId,
    string TermName,
    int Capacity,
    string Room)
{
    public string TeacherName => $"{TeacherFirstName} {TeacherLastName}".Trim();
}

/// <summary>A single student enrolled in an offering. Backed by enrollments INNER JOIN students.</summary>
public sealed record RosterEntryDto(
    int EnrollmentId,
    int StudentId,
    string FirstName,
    string LastName,
    string Email,
    DateOnly EnrolledAt,
    string? Grade)
{
    public string FullName => $"{FirstName} {LastName}".Trim();
}
