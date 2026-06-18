using System.Security.Claims;
using DtoOrm.Api.Application.Common;
using DtoOrm.Api.Infrastructure.Auth;

namespace DtoOrm.Api.Features.Students;

public static class StudentsEndpoints
{
    public static IEndpointRouteBuilder MapStudents(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/students").WithTags("Students");

        group.MapGet("/", async (string? lastNameLike, bool? isActive, int? take, int? skip, Dispatcher d, CancellationToken ct) =>
            Results.Ok(await d.QueryAsync(new ListStudentsQuery(lastNameLike, isActive, take ?? 50, skip ?? 0), ct)))
            .RequireAuthorization("AdminOrTeacher");
        group.RequireAuthorization();

        group.MapGet("/{id:int}", async (int id, ClaimsPrincipal user, Dispatcher d, CancellationToken ct) =>
        {
            if (!user.CanAccessStudent(id))
            {
                return Results.Forbid();
            }

            var result = await d.QueryAsync(new GetStudentByIdQuery(id), ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        group.MapGet("/email/{email}", async (string email, Dispatcher d, CancellationToken ct) =>
        {
            var result = await d.QueryAsync(new GetStudentByEmailQuery(email), ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).RequireAuthorization("AdminOrTeacher");

        group.MapGet("/me", async (ClaimsPrincipal user, Dispatcher d, CancellationToken ct) =>
        {
            var studentId = user.GetStudentId();
            if (studentId is null)
            {
                return Results.Forbid();
            }

            var result = await d.QueryAsync(new GetStudentByIdQuery(studentId.Value), ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        group.MapPost("/", async (CreateStudentCommand body, Dispatcher d, CancellationToken ct) =>
        {
            var id = await d.SendAsync(body, ct);
            return Results.Created($"/api/students/{id}", new { id });
        }).RequireAuthorization("AdminOnly");

        group.MapPut("/{id:int}", async (int id, UpdateStudentCommand body, Dispatcher d, CancellationToken ct) =>
        {
            var updated = await d.SendAsync(body with { Id = id }, ct);
            return updated ? Results.NoContent() : Results.NotFound();
        }).RequireAuthorization("AdminOnly");

        group.MapDelete("/{id:int}", async (int id, Dispatcher d, CancellationToken ct) =>
        {
            var deleted = await d.SendAsync(new DeleteStudentCommand(id), ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        }).RequireAuthorization("AdminOnly");

        return app;
    }
}
