using DtoOrm.Portal.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DtoOrm.Portal.Pages.Offerings;

public sealed class IndexModel : ApiPageModel
{
    private readonly ISchoolApiClient _api;

    public IndexModel(ISchoolApiClient api) => _api = api;

    public IReadOnlyList<OfferingSeatsItem> Offerings { get; private set; } = Array.Empty<OfferingSeatsItem>();

    public IEnumerable<IGrouping<string, OfferingSeatsItem>> ByTerm =>
        Offerings.GroupBy(o => o.TermName);

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await TryAsync(async () =>
        {
            Offerings = await _api.GetOfferingSeatsAsync(cancellationToken: cancellationToken);
        });
    }
}
