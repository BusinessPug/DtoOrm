using DtoOrm.Portal.Services;

namespace DtoOrm.Portal.Pages;

public sealed class IndexModel : ApiPageModel
{
    private const int ActiveTermId = 3;

    private readonly ISchoolApiClient _api;

    public IndexModel(ISchoolApiClient api) => _api = api;

    public IReadOnlyList<DepartmentCatalogItem> Departments { get; private set; } = Array.Empty<DepartmentCatalogItem>();
    public IReadOnlyList<PopularCourseItem> PopularCourses { get; private set; } = Array.Empty<PopularCourseItem>();
    public IReadOnlyList<OfferingSeatsItem> Offerings { get; private set; } = Array.Empty<OfferingSeatsItem>();
    public IReadOnlyList<ScheduleItem> Schedule { get; private set; } = Array.Empty<ScheduleItem>();
    public WeeklyScheduleGrid WeekGrid { get; private set; } = WeeklyScheduleGridBuilder.Build([]);
    public IReadOnlyList<AcademicBreakItem> Breaks { get; private set; } = Array.Empty<AcademicBreakItem>();

    public long TotalCourses => Departments.Sum(d => d.CourseCount);
    public long TotalEnrollments => PopularCourses.Sum(c => c.EnrollmentCount);
    public string Role => User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "User";
    public DateOnly Today => DateOnly.FromDateTime(DateTime.Today);
    public int TodayIsoDay => ((int)DateTime.Today.DayOfWeek + 6) % 7 + 1;
    public IReadOnlyList<ScheduleItem> TodaySchedule => Schedule
        .Where(item => item.DayOfWeek == TodayIsoDay)
        .OrderBy(item => item.StartsAt)
        .ToList();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await TryAsync(async () =>
        {
            Departments = await _api.GetDepartmentCatalogAsync(cancellationToken);
            Offerings = await _api.GetOfferingSeatsAsync(termId: ActiveTermId, cancellationToken: cancellationToken);
            Schedule = await _api.GetMyScheduleAsync(cancellationToken);
            WeekGrid = WeeklyScheduleGridBuilder.Build(Schedule);
            Breaks = await _api.GetAcademicBreaksAsync(cancellationToken);

            if (User.IsInRole("Administrator") || User.IsInRole("Teacher"))
            {
                PopularCourses = await _api.GetPopularCoursesAsync(minEnrollments: 1, take: 8, cancellationToken: cancellationToken);
            }
        });
    }
}
