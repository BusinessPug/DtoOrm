using DtoOrm.Api.Application.Common;

namespace DtoOrm.Api.Features.Offerings;

public static class OfferingsEndpoints
{
    public static IEndpointRouteBuilder MapOfferings(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/offerings").WithTags("Offerings");

        group.MapGet("/", async (int? courseId, int? termId, int? teacherId, int? take, int? skip, Dispatcher d, CancellationToken ct) =>
            Results.Ok(await d.QueryAsync(new ListOfferingsQuery(courseId, termId, teacherId, take ?? 100, skip ?? 0), ct)));

        group.MapGet("/{id:int}", async (int id, Dispatcher d, CancellationToken ct) =>
        {
            var result = await d.QueryAsync(new GetOfferingByIdQuery(id), ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        group.MapPost("/", async (CreateOfferingCommand body, Dispatcher d, CancellationToken ct) =>
        {
            var id = await d.SendAsync(body, ct);
            return Results.Created($"/api/offerings/{id}", new { id });
        });

        group.MapPut("/{id:int}", async (int id, UpdateOfferingCommand body, Dispatcher d, CancellationToken ct) =>
        {
            var updated = await d.SendAsync(body with { Id = id }, ct);
            return updated ? Results.NoContent() : Results.NotFound();
        });

        group.MapDelete("/{id:int}", async (int id, Dispatcher d, CancellationToken ct) =>
        {
            var deleted = await d.SendAsync(new DeleteOfferingCommand(id), ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        });

        return app;
    }
}
