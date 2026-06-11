using DtoOrm.Api.Application.Common;

namespace DtoOrm.Api.Features.Reports;

public sealed record DepartmentCatalogQuery : IQuery<IReadOnlyList<DepartmentCatalogDto>>;

public sealed record CoursePopularityQuery(long MinEnrollments = 1, int Take = 20)
    : IQuery<IReadOnlyList<CoursePopularityDto>>;

public sealed record OfferingSeatsQuery(int? TermId = null)
    : IQuery<IReadOnlyList<OfferingSeatsDto>>;
