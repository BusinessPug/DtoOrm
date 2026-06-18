using DtoOrm.Api.Application.Common;
using DtoOrm.Api.Infrastructure.Auth;

namespace DtoOrm.Api.Features.Departments;

public static class DepartmentsEndpoints
{
    public static IEndpointRouteBuilder MapDepartments(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/departments").WithTags("Departments");

        group.MapGet("/", async (Dispatcher d, CancellationToken ct) =>
            Results.Ok(await d.QueryAsync(new ListDepartmentsQuery(), ct)));

        group.MapGet("/{id:int}", async (int id, Dispatcher d, CancellationToken ct) =>
        {
            var result = await d.QueryAsync(new GetDepartmentByIdQuery(id), ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        group.RequireAuthorization();

        group.MapPost("/", async (CreateDepartmentCommand body, Dispatcher d, CancellationToken ct) =>
        {
            var id = await d.SendAsync(body, ct);
            return Results.Created($"/api/departments/{id}", new { id });
        }).RequireAuthorization("AdminOnly");

        group.MapPut("/{id:int}", async (int id, UpdateDepartmentCommand body, Dispatcher d, CancellationToken ct) =>
        {
            var updated = await d.SendAsync(body with { Id = id }, ct);
            return updated ? Results.NoContent() : Results.NotFound();
        }).RequireAuthorization("AdminOnly");

        group.MapDelete("/{id:int}", async (int id, Dispatcher d, CancellationToken ct) =>
        {
            var deleted = await d.SendAsync(new DeleteDepartmentCommand(id), ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        }).RequireAuthorization("AdminOnly");

        return app;
    }
}
