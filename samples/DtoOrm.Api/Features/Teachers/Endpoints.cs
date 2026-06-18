using DtoOrm.Api.Application.Common;

namespace DtoOrm.Api.Features.Teachers;

public static class TeachersEndpoints
{
    public static IEndpointRouteBuilder MapTeachers(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/teachers").WithTags("Teachers");
        group.RequireAuthorization("AdminOrTeacher");

        group.MapGet("/", async (int? departmentId, bool? isActive, int? take, int? skip, Dispatcher d, CancellationToken ct) =>
            Results.Ok(await d.QueryAsync(new ListTeachersQuery(departmentId, isActive, take ?? 100, skip ?? 0), ct)));

        group.MapGet("/{id:int}", async (int id, Dispatcher d, CancellationToken ct) =>
        {
            var result = await d.QueryAsync(new GetTeacherByIdQuery(id), ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        group.MapPost("/", async (CreateTeacherCommand body, Dispatcher d, CancellationToken ct) =>
        {
            var id = await d.SendAsync(body, ct);
            return Results.Created($"/api/teachers/{id}", new { id });
        }).RequireAuthorization("AdminOnly");

        group.MapPut("/{id:int}", async (int id, UpdateTeacherCommand body, Dispatcher d, CancellationToken ct) =>
        {
            var updated = await d.SendAsync(body with { Id = id }, ct);
            return updated ? Results.NoContent() : Results.NotFound();
        }).RequireAuthorization("AdminOnly");

        group.MapDelete("/{id:int}", async (int id, Dispatcher d, CancellationToken ct) =>
        {
            var deleted = await d.SendAsync(new DeleteTeacherCommand(id), ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        }).RequireAuthorization("AdminOnly");

        return app;
    }
}
