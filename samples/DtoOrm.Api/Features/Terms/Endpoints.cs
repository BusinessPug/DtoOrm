using DtoOrm.Api.Application.Common;

namespace DtoOrm.Api.Features.Terms;

public static class TermsEndpoints
{
    public static IEndpointRouteBuilder MapTerms(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/terms").WithTags("Terms");
        group.RequireAuthorization();

        group.MapGet("/", async (Dispatcher d, CancellationToken ct) =>
            Results.Ok(await d.QueryAsync(new ListTermsQuery(), ct)));

        group.MapGet("/{id:int}", async (int id, Dispatcher d, CancellationToken ct) =>
        {
            var result = await d.QueryAsync(new GetTermByIdQuery(id), ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        return app;
    }
}
