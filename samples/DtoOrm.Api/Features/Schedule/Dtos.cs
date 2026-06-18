namespace DtoOrm.Api.Features.Schedule;

public sealed record ScheduleItemDto(
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
}

public sealed record AcademicBreakDto(
    int Id,
    int TermId,
    string TermName,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    string? Notes);
