using System.Security.Claims;
using DtoOrm.Api.Application.Common;
using DtoOrm.Api.Infrastructure.Auth;

namespace DtoOrm.Api.Features.Schedule;

public static class ScheduleEndpoints
{
    public static IEndpointRouteBuilder MapSchedule(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/schedule")
            .WithTags("Schedule")
            .RequireAuthorization();

        group.MapGet("/", async (int? studentId, int? teacherId, int? termId, ClaimsPrincipal user, Dispatcher d, CancellationToken ct) =>
        {
            if (studentId is not null && !user.CanAccessStudent(studentId.Value))
            {
                return Results.Forbid();
            }

            if (teacherId is not null && !user.CanAccessTeacherWork(teacherId.Value))
            {
                return Results.Forbid();
            }

            if (user.IsStudent())
            {
                studentId = user.GetStudentId();
                teacherId = null;
            }
            else if (user.IsTeacher() && teacherId is null)
            {
                teacherId = user.GetTeacherId();
            }

            return Results.Ok(await d.QueryAsync(new ScheduleQuery(studentId, teacherId, termId), ct));
        });

        group.MapGet("/me", async (int? termId, ClaimsPrincipal user, Dispatcher d, CancellationToken ct) =>
        {
            var query = user.IsStudent()
                ? new ScheduleQuery(StudentId: user.GetStudentId(), TermId: termId)
                : user.IsTeacher()
                    ? new ScheduleQuery(TeacherId: user.GetTeacherId(), TermId: termId)
                    : new ScheduleQuery(TermId: termId);

            return Results.Ok(await d.QueryAsync(query, ct));
        });

        group.MapGet("/breaks", async (int? termId, Dispatcher d, CancellationToken ct) =>
            Results.Ok(await d.QueryAsync(new AcademicBreaksQuery(termId), ct)));

        return app;
    }
}
