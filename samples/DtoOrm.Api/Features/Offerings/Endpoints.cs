using System.Security.Claims;
using DtoOrm.Api.Application.Common;
using DtoOrm.Api.Infrastructure.Auth;

namespace DtoOrm.Api.Features.Offerings;

public static class OfferingsEndpoints
{
    public static IEndpointRouteBuilder MapOfferings(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/offerings").WithTags("Offerings");
        group.RequireAuthorization();

        group.MapGet("/", async (int? courseId, int? termId, int? teacherId, int? take, int? skip, ClaimsPrincipal user, Dispatcher d, CancellationToken ct) =>
        {
            if (user.IsTeacher())
            {
                teacherId = user.GetTeacherId();
            }

            return Results.Ok(await d.QueryAsync(new ListOfferingsQuery(courseId, termId, teacherId, take ?? 100, skip ?? 0), ct));
        });

        group.MapGet("/{id:int}", async (int id, Dispatcher d, CancellationToken ct) =>
        {
            var result = await d.QueryAsync(new GetOfferingByIdQuery(id), ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        group.MapGet("/{id:int}/details", async (int id, Dispatcher d, CancellationToken ct) =>
        {
            var result = await d.QueryAsync(new GetOfferingDetailsQuery(id), ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        group.MapGet("/{id:int}/roster", async (int id, ClaimsPrincipal user, Dispatcher d, CancellationToken ct) =>
        {
            var offering = await d.QueryAsync(new GetOfferingByIdQuery(id), ct);
            if (offering is null)
            {
                return Results.NotFound();
            }

            if (!user.IsAdministrator() && user.GetTeacherId() != offering.TeacherId)
            {
                return Results.Forbid();
            }

            return Results.Ok(await d.QueryAsync(new GetOfferingRosterQuery(id), ct));
        }).RequireAuthorization("AdminOrTeacher");

        group.MapPost("/", async (CreateOfferingCommand body, ClaimsPrincipal user, Dispatcher d, CancellationToken ct) =>
        {
            if (user.IsTeacher())
            {
                var teacherId = user.GetTeacherId();
                if (teacherId is null || (body.TeacherId != 0 && body.TeacherId != teacherId.Value))
                {
                    return Results.Forbid();
                }

                body = body with { TeacherId = teacherId.Value };
            }

            var id = await d.SendAsync(body, ct);
            return Results.Created($"/api/offerings/{id}", new { id });
        }).RequireAuthorization("AdminOrTeacher");

        group.MapPut("/{id:int}", async (int id, UpdateOfferingCommand body, ClaimsPrincipal user, Dispatcher d, CancellationToken ct) =>
        {
            if (user.IsTeacher())
            {
                var teacherId = user.GetTeacherId();
                var current = await d.QueryAsync(new GetOfferingByIdQuery(id), ct);
                if (teacherId is null || current is null)
                {
                    return current is null ? Results.NotFound() : Results.Forbid();
                }

                if (current.TeacherId != teacherId.Value || (body.TeacherId != 0 && body.TeacherId != teacherId.Value))
                {
                    return Results.Forbid();
                }

                body = body with { TeacherId = teacherId.Value };
            }

            var updated = await d.SendAsync(body with { Id = id }, ct);
            return updated ? Results.NoContent() : Results.NotFound();
        }).RequireAuthorization("AdminOrTeacher");

        group.MapDelete("/{id:int}", async (int id, ClaimsPrincipal user, Dispatcher d, CancellationToken ct) =>
        {
            if (user.IsTeacher())
            {
                var current = await d.QueryAsync(new GetOfferingByIdQuery(id), ct);
                if (current is null)
                {
                    return Results.NotFound();
                }

                if (user.GetTeacherId() != current.TeacherId)
                {
                    return Results.Forbid();
                }
            }

            var deleted = await d.SendAsync(new DeleteOfferingCommand(id), ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        }).RequireAuthorization("AdminOrTeacher");

        return app;
    }
}
