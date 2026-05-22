using DtoOrm.Api.Application.Common;

namespace DtoOrm.Api.Features.Students;

public static class StudentsEndpoints
{
    public static IEndpointRouteBuilder MapStudents(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/students").WithTags("Students");

        group.MapGet("/", async (string? lastNameLike, bool? isActive, int? take, int? skip, Dispatcher d, CancellationToken ct) =>
            Results.Ok(await d.QueryAsync(new ListStudentsQuery(lastNameLike, isActive, take ?? 50, skip ?? 0), ct)));

        group.MapGet("/{id:int}", async (int id, Dispatcher d, CancellationToken ct) =>
        {
            var result = await d.QueryAsync(new GetStudentByIdQuery(id), ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        group.MapGet("/email/{email}", async (string email, Dispatcher d, CancellationToken ct) =>
        {
            var result = await d.QueryAsync(new GetStudentByEmailQuery(email), ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        group.MapPost("/", async (CreateStudentCommand body, Dispatcher d, CancellationToken ct) =>
        {
            var id = await d.SendAsync(body, ct);
            return Results.Created($"/api/students/{id}", new { id });
        });

        group.MapPut("/{id:int}", async (int id, UpdateStudentCommand body, Dispatcher d, CancellationToken ct) =>
        {
            var updated = await d.SendAsync(body with { Id = id }, ct);
            return updated ? Results.NoContent() : Results.NotFound();
        });

        group.MapDelete("/{id:int}", async (int id, Dispatcher d, CancellationToken ct) =>
        {
            var deleted = await d.SendAsync(new DeleteStudentCommand(id), ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        });

        return app;
    }
}
