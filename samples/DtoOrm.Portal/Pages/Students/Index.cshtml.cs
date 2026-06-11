using DtoOrm.Portal.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DtoOrm.Portal.Pages.Students;

public sealed class IndexModel : ApiPageModel
{
    private readonly ISchoolApiClient _api;

    public IndexModel(ISchoolApiClient api) => _api = api;

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public string Status { get; set; } = "active";

    public IReadOnlyList<StudentItem> Students { get; private set; } = Array.Empty<StudentItem>();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        bool? isActive = Status switch
        {
            "active" => true,
            "inactive" => false,
            _ => null
        };

        await TryAsync(async () =>
        {
            Students = await _api.GetStudentsAsync(
                lastNameLike: string.IsNullOrWhiteSpace(Search) ? null : Search,
                isActive: isActive,
                take: 200,
                cancellationToken: cancellationToken);
        });
    }
}
