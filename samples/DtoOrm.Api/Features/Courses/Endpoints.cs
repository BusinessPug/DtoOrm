using DtoOrm.Api.Application.Common;

namespace DtoOrm.Api.Features.Courses;

public static class CoursesEndpoints
{
    public static IEndpointRouteBuilder MapCourses(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/courses").WithTags("Courses");

        group.MapGet("/", async (int? departmentId, int? take, int? skip, Dispatcher d, CancellationToken ct) =>
            Results.Ok(await d.QueryAsync(new ListCoursesQuery(departmentId, take ?? 100, skip ?? 0), ct)));

        group.MapGet("/{id:int}", async (int id, Dispatcher d, CancellationToken ct) =>
        {
            var result = await d.QueryAsync(new GetCourseByIdQuery(id), ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        group.MapPost("/", async (CreateCourseCommand body, Dispatcher d, CancellationToken ct) =>
        {
            var id = await d.SendAsync(body, ct);
            return Results.Created($"/api/courses/{id}", new { id });
        });

        group.MapPut("/{id:int}", async (int id, UpdateCourseCommand body, Dispatcher d, CancellationToken ct) =>
        {
            var updated = await d.SendAsync(body with { Id = id }, ct);
            return updated ? Results.NoContent() : Results.NotFound();
        });

        group.MapDelete("/{id:int}", async (int id, Dispatcher d, CancellationToken ct) =>
        {
            var deleted = await d.SendAsync(new DeleteCourseCommand(id), ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        });

        return app;
    }
}
