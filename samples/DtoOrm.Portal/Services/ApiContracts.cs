namespace DtoOrm.Portal.Services;

// Portal-side projections of the School API responses. They are intentionally separate from the API
// DTOs: the portal depends only on the HTTP contract, never on the API assembly. Property names match
// the JSON the API emits (compared case-insensitively by System.Text.Json web defaults).

public sealed record LoginRequest(string Username, string Password);

public sealed record LoginResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    CurrentUserItem User);

public sealed record CurrentUserItem(
    int Id,
    string Username,
    string Email,
    string DisplayName,
    string Role,
    int? StudentId,
    int? TeacherId);

public sealed record CourseItem(int Id, string Code, string Title, int Credits, int DepartmentId);

public sealed record TermItem(int Id, string Code, string Name, DateOnly StartDate, DateOnly EndDate);

public sealed record TeacherItem(
    int Id,
    string FirstName,
    string LastName,
    string Email,
    int DepartmentId,
    DateOnly HiredAt,
    bool IsActive)
{
    public string FullName => $"{FirstName} {LastName}".Trim();
}

public sealed record OfferingItem(
    int Id,
    int CourseId,
    int TeacherId,
    int TermId,
    int Capacity,
    string Room,
    string? Notes);

public sealed record SaveOfferingRequest(
    int CourseId,
    int TeacherId,
    int TermId,
    int Capacity,
    string Room,
    string? Notes);

/// <summary>Course count per department (from <c>/api/reports/department-catalog</c>).</summary>
public sealed record DepartmentCatalogItem(
    int DepartmentId,
    string Code,
    string Name,
    long CourseCount);

/// <summary>A course ranked by enrollments (from <c>/api/reports/popular-courses</c>).</summary>
public sealed record PopularCourseItem(
    int CourseId,
    string Code,
    string Title,
    long EnrollmentCount);

/// <summary>Seat utilisation for an offering (from <c>/api/reports/offering-seats</c>).</summary>
public sealed record OfferingSeatsItem(
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
    public int FillPercent => Capacity <= 0 ? 0 : (int)Math.Min(100, Math.Round(Enrolled * 100.0 / Capacity));
}

/// <summary>A student record (from <c>/api/students</c>).</summary>
public sealed record StudentItem(
    int Id,
    string FirstName,
    string LastName,
    string Email,
    DateOnly DateOfBirth,
    DateOnly EnrolledAt,
    bool IsActive)
{
    public string FullName => $"{FirstName} {LastName}".Trim();
}

/// <summary>A single transcript line for a student (from <c>/api/enrollments/transcript/{id}</c>).</summary>
public sealed record TranscriptItem(
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
    string? Grade)
{
    public string TeacherName => $"{TeacherFirstName} {TeacherLastName}".Trim();
    public bool IsGraded => !string.IsNullOrWhiteSpace(Grade);
}

/// <summary>A fully resolved offering (from <c>/api/offerings/{id}/details</c>).</summary>
public sealed record OfferingDetailsItem(
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
    string Room,
    string? Notes)
{
    public string TeacherName => $"{TeacherFirstName} {TeacherLastName}".Trim();
}

/// <summary>A student enrolled in an offering (from <c>/api/offerings/{id}/roster</c>).</summary>
public sealed record RosterItem(
    int EnrollmentId,
    int StudentId,
    string FirstName,
    string LastName,
    string Email,
    DateOnly EnrolledAt,
    string? Grade)
{
    public string FullName => $"{FirstName} {LastName}".Trim();
    public bool IsGraded => !string.IsNullOrWhiteSpace(Grade);
}

public sealed record ScheduleItem(
    int MeetingId,
    int OfferingId,
    int CourseId,
    string CourseCode,
    string CourseTitle,
    int TermId,
    string TermName,
    int TeacherId,
    string TeacherFirstName,
    string TeacherLastName,
    sbyte DayOfWeek,
    TimeSpan StartsAt,
    TimeSpan EndsAt,
    string Location)
{
    public string TeacherName => $"{TeacherFirstName} {TeacherLastName}".Trim();
    public string DayName => DayOfWeek switch
    {
        1 => "Monday",
        2 => "Tuesday",
        3 => "Wednesday",
        4 => "Thursday",
        5 => "Friday",
        6 => "Saturday",
        7 => "Sunday",
        _ => "Scheduled"
    };

    public string TimeRange => $"{StartsAt:hh\\:mm}-{EndsAt:hh\\:mm}";
}

public sealed record AcademicBreakItem(
    int Id,
    int TermId,
    string TermName,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    string? Notes);
