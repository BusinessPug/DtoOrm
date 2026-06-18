using DtoOrm.Portal.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DtoOrm.Portal.Pages.Offerings;

public sealed class DetailsModel : ApiPageModel
{
    private readonly ISchoolApiClient _api;

    public DetailsModel(ISchoolApiClient api) => _api = api;

    public OfferingDetailsItem? Offering { get; private set; }
    public IReadOnlyList<RosterItem> Roster { get; private set; } = Array.Empty<RosterItem>();

    public bool CanViewRoster =>
        User.IsInRole("Administrator") ||
        (Offering is not null && User.GetTeacherId() == Offering.TeacherId);
    public int GradedCount => Roster.Count(r => r.IsGraded);
    public int SeatsRemaining => Offering is null ? 0 : Math.Max(0, Offering.Capacity - Roster.Count);

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        var reached = await TryAsync(async () =>
        {
            Offering = await _api.GetOfferingDetailsAsync(id, cancellationToken);
            if (Offering is not null && CanViewRoster)
            {
                Roster = await _api.GetOfferingRosterAsync(id, cancellationToken);
            }
        });

        if (reached && Offering is null)
        {
            return NotFound();
        }

        return Page();
    }
}
