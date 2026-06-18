using DtoOrm.Portal.Services;
using Microsoft.AspNetCore.Mvc;

namespace DtoOrm.Portal.Pages.Students;

public sealed class ScheduleModel : ApiPageModel
{
    private readonly ISchoolApiClient _api;

    public ScheduleModel(ISchoolApiClient api) => _api = api;

    public StudentItem? Student { get; private set; }
    public IReadOnlyList<ScheduleItem> Schedule { get; private set; } = Array.Empty<ScheduleItem>();
    public WeeklyScheduleGrid WeekGrid { get; private set; } = WeeklyScheduleGridBuilder.Build([]);

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        if (!User.IsInRole("Administrator") && !User.IsInRole("Teacher"))
        {
            return Forbid();
        }

        var reached = await TryAsync(async () =>
        {
            Student = await _api.GetStudentAsync(id, cancellationToken);
            if (Student is not null)
            {
                Schedule = await _api.GetStudentScheduleAsync(id, cancellationToken);
                WeekGrid = WeeklyScheduleGridBuilder.Build(Schedule);
            }
        });

        if (reached && Student is null)
        {
            return NotFound();
        }

        return Page();
    }
}
