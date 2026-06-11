namespace DtoOrm.Api.Features.Reports;

/// <summary>How many courses each department owns. Backed by a LEFT JOIN + COUNT + GROUP BY query.</summary>
public sealed record DepartmentCatalogDto(
    int DepartmentId,
    string Code,
    string Name,
    long CourseCount);

/// <summary>Courses ranked by total enrollments. Backed by INNER JOINs + GROUP BY + HAVING + ORDER BY.</summary>
public sealed record CoursePopularityDto(
    int CourseId,
    string Code,
    string Title,
    long EnrollmentCount);

/// <summary>
/// Seat utilisation for a course offering. Backed by three INNER JOINs, a LEFT JOIN to enrollments,
/// COUNT and GROUP BY. <see cref="SeatsAvailable"/> and <see cref="TeacherName"/> are derived on read.
/// </summary>
public sealed record OfferingSeatsDto(
    int OfferingId,
    string CourseCode,
    string CourseTitle,
    string TermName,
    string TeacherFirstName,
    string TeacherLastName,
    string Room,
    int Capacity,
    long Enrolled)
{
    public string TeacherName => $"{TeacherFirstName} {TeacherLastName}".Trim();

    public long SeatsAvailable => Math.Max(0, Capacity - Enrolled);

    public bool IsFull => Enrolled >= Capacity;
}
