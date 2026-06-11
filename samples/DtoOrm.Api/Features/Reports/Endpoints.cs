using DtoOrm.Api.Application.Common;

namespace DtoOrm.Api.Features.Reports;

public static class ReportsEndpoints
{
    public static IEndpointRouteBuilder MapReports(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reports").WithTags("Reports");

        // Course count per department (LEFT JOIN + COUNT + GROUP BY).
        group.MapGet("/department-catalog", async (Dispatcher d, CancellationToken ct) =>
            Results.Ok(await d.QueryAsync(new DepartmentCatalogQuery(), ct)));

        // Courses ranked by enrollments (INNER JOIN + GROUP BY + HAVING + ORDER BY DESC).
        group.MapGet("/popular-courses", async (long? minEnrollments, int? take, Dispatcher d, CancellationToken ct) =>
            Results.Ok(await d.QueryAsync(new CoursePopularityQuery(minEnrollments ?? 1, take ?? 20), ct)));

        // Seat utilisation per offering (3x INNER JOIN + LEFT JOIN + COUNT + GROUP BY).
        group.MapGet("/offering-seats", async (int? termId, Dispatcher d, CancellationToken ct) =>
            Results.Ok(await d.QueryAsync(new OfferingSeatsQuery(termId), ct)));

        return app;
    }
}
