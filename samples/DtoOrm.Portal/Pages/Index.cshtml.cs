using DtoOrm.Portal.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DtoOrm.Portal.Pages;

public sealed class IndexModel : ApiPageModel
{
    private readonly ISchoolApiClient _api;

    public IndexModel(ISchoolApiClient api) => _api = api;

    public IReadOnlyList<DepartmentCatalogItem> Departments { get; private set; } = Array.Empty<DepartmentCatalogItem>();
    public IReadOnlyList<PopularCourseItem> PopularCourses { get; private set; } = Array.Empty<PopularCourseItem>();
    public IReadOnlyList<OfferingSeatsItem> Offerings { get; private set; } = Array.Empty<OfferingSeatsItem>();

    public long TotalCourses => Departments.Sum(d => d.CourseCount);
    public long TotalEnrollments => PopularCourses.Sum(c => c.EnrollmentCount);

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await TryAsync(async () =>
        {
            Departments = await _api.GetDepartmentCatalogAsync(cancellationToken);
            PopularCourses = await _api.GetPopularCoursesAsync(minEnrollments: 1, take: 8, cancellationToken: cancellationToken);
            Offerings = await _api.GetOfferingSeatsAsync(cancellationToken: cancellationToken);
        });
    }
}
