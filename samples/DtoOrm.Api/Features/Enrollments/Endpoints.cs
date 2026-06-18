using System.Security.Claims;
using DtoOrm.Api.Application.Common;
using DtoOrm.Api.Features.Offerings;
using DtoOrm.Api.Infrastructure.Auth;

namespace DtoOrm.Api.Features.Enrollments;

public static class EnrollmentsEndpoints
{
    public static IEndpointRouteBuilder MapEnrollments(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/enrollments").WithTags("Enrollments");
        group.RequireAuthorization();

        group.MapGet("/", async (int? studentId, int? offeringId, int? take, int? skip, ClaimsPrincipal user, Dispatcher d, CancellationToken ct) =>
        {
            if (user.IsStudent())
            {
                studentId = user.GetStudentId();
            }
            else if (!user.IsAdministrator() && !user.IsTeacher())
            {
                return Results.Forbid();
            }

            return Results.Ok(await d.QueryAsync(new ListEnrollmentsQuery(studentId, offeringId, take ?? 200, skip ?? 0), ct));
        });

        group.MapPost("/", async (EnrollStudentCommand body, Dispatcher d, CancellationToken ct) =>
        {
            var outcome = await d.SendAsync(body, ct);
            return outcome.Status switch
            {
                EnrollmentStatus.Enrolled => Results.Created($"/api/enrollments/{outcome.EnrollmentId}", new { id = outcome.EnrollmentId }),
                EnrollmentStatus.AlreadyEnrolled => Results.Conflict(new { message = "Student is already enrolled in this offering.", enrollmentId = outcome.EnrollmentId }),
                EnrollmentStatus.OfferingFull => Results.Conflict(new { message = "This offering is full.", capacity = outcome.Capacity, enrolled = outcome.Enrolled }),
                EnrollmentStatus.OfferingNotFound => Results.NotFound(new { message = "Offering not found." }),
                _ => Results.Problem("Unknown enrollment outcome.")
            };
        }).RequireAuthorization("AdminOnly");

        group.MapPatch("/{id:int}/grade", async (int id, AssignGradeCommand body, ClaimsPrincipal user, Dispatcher d, CancellationToken ct) =>
        {
            var enrollment = await d.QueryAsync(new GetEnrollmentByIdQuery(id), ct);
            if (enrollment is null)
            {
                return Results.NotFound();
            }

            if (user.IsTeacher())
            {
                var offering = await d.QueryAsync(new GetOfferingByIdQuery(enrollment.OfferingId), ct);
                if (offering is null || user.GetTeacherId() != offering.TeacherId)
                {
                    return Results.Forbid();
                }
            }

            var ok = await d.SendAsync(body with { EnrollmentId = id }, ct);
            return ok ? Results.NoContent() : Results.NotFound();
        }).RequireAuthorization("AdminOrTeacher");

        group.MapDelete("/{id:int}", async (int id, Dispatcher d, CancellationToken ct) =>
        {
            var ok = await d.SendAsync(new DropEnrollmentCommand(id), ct);
            return ok ? Results.NoContent() : Results.NotFound();
        }).RequireAuthorization("AdminOnly");

        app.MapGet("/api/enrollments/transcript/{id:int}", async (int id, ClaimsPrincipal user, Dispatcher d, CancellationToken ct) =>
        {
            if (!user.CanAccessStudent(id))
            {
                return Results.Forbid();
            }

            return Results.Ok(await d.QueryAsync(new GetStudentTranscriptQuery(id), ct));
        })
           .WithTags("Students")
           .RequireAuthorization();

        return app;
    }
}
