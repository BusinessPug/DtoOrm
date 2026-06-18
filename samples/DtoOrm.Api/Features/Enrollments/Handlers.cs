using DtoOrm.Api.Generated;
using DtoOrm.Api.Application.Common;
using DtoOrm.Core;

namespace DtoOrm.Api.Features.Enrollments;

public sealed class ListEnrollmentsHandler : IQueryHandler<ListEnrollmentsQuery, IReadOnlyList<EnrollmentDto>>
{
    private readonly OrmSession _session;
    public ListEnrollmentsHandler(OrmSession session) => _session = session;

    public Task<IReadOnlyList<EnrollmentDto>> HandleAsync(ListEnrollmentsQuery query, CancellationToken cancellationToken)
    {
        var e = Db.Tables.Enrollments;
        var builder = _session
            .From(e)
            .Select(e.Id, e.StudentId, e.OfferingId, e.EnrolledAt, e.Grade);

        SqlCondition? where = null;
        if (query.StudentId is not null)
        {
            where = e.StudentId.Eq(query.StudentId.Value);
        }
        if (query.OfferingId is not null)
        {
            var c = e.OfferingId.Eq(query.OfferingId.Value);
            where = where is null ? c : where & c;
        }
        if (where is not null)
        {
            builder = builder.Where(where);
        }

        return builder.Take(query.Take).Skip(query.Skip).ToListAsync<EnrollmentDto>(cancellationToken);
    }
}

public sealed class GetStudentTranscriptHandler : IQueryHandler<GetStudentTranscriptQuery, IReadOnlyList<TranscriptEntryDto>>
{
    private readonly OrmSession _session;
    public GetStudentTranscriptHandler(OrmSession session) => _session = session;

    public Task<IReadOnlyList<TranscriptEntryDto>> HandleAsync(GetStudentTranscriptQuery query, CancellationToken cancellationToken)
    {
        var e = Db.Tables.Enrollments;
        var o = Db.Tables.Offerings;
        var c = Db.Tables.Courses;
        var tm = Db.Tables.Terms;
        var t = Db.Tables.Teachers;

        return _session
            .From(e)
            .InnerJoin(o, o.Id.EqColumn(e.OfferingId))
            .InnerJoin(c, c.Id.EqColumn(o.CourseId))
            .InnerJoin(tm, tm.Id.EqColumn(o.TermId))
            .InnerJoin(t, t.Id.EqColumn(o.TeacherId))
            .Select(
                e.Id.As("EnrollmentId"),
                e.OfferingId.As("OfferingId"),
                c.Id.As("CourseId"),
                c.Code.As("CourseCode"),
                c.Title.As("CourseTitle"),
                c.Credits.As("Credits"),
                tm.Id.As("TermId"),
                tm.Code.As("TermCode"),
                tm.Name.As("TermName"),
                t.Id.As("TeacherId"),
                t.FirstName.As("TeacherFirstName"),
                t.LastName.As("TeacherLastName"),
                e.EnrolledAt.As("EnrolledAt"),
                e.Grade.As("Grade"))
            .Where(e.StudentId.Eq(query.StudentId))
            .OrderBy(tm.StartDate)
            .OrderBy(c.Code)
            .ToListAsync<TranscriptEntryDto>(cancellationToken);
    }
}

public sealed class GetEnrollmentByIdHandler : IQueryHandler<GetEnrollmentByIdQuery, EnrollmentDto?>
{
    private readonly OrmSession _session;
    public GetEnrollmentByIdHandler(OrmSession session) => _session = session;

    public Task<EnrollmentDto?> HandleAsync(GetEnrollmentByIdQuery query, CancellationToken cancellationToken)
    {
        var e = Db.Tables.Enrollments;
        return _session
            .From(e)
            .Select(e.Id, e.StudentId, e.OfferingId, e.EnrolledAt, e.Grade)
            .Where(e.Id.Eq(query.Id))
            .SingleOrDefaultAsync<EnrollmentDto>(cancellationToken);
    }
}

public sealed class EnrollStudentHandler : ICommandHandler<EnrollStudentCommand, EnrollmentOutcome>
{
    private readonly OrmSession _session;
    public EnrollStudentHandler(OrmSession session) => _session = session;

    public Task<EnrollmentOutcome> HandleAsync(EnrollStudentCommand command, CancellationToken cancellationToken)
    {
        var e = Db.Tables.Enrollments;
        var o = Db.Tables.Offerings;

        // Run the read-checks and the insert on one connection inside a transaction so the seat
        // count cannot change between the capacity check and the insert.
        return _session.WithTransactionAsync(async tx =>
        {
            var existing = await tx
                .From(e)
                .Select(e.Id.As("Id"))
                .Where(e.StudentId.Eq(command.StudentId) & e.OfferingId.Eq(command.OfferingId))
                .FirstOrDefaultAsync<IdRow>(cancellationToken).ConfigureAwait(false);
            if (existing is not null)
            {
                return EnrollmentOutcome.AlreadyEnrolled(existing.Id);
            }

            var offering = await tx
                .From(o)
                .Select(o.Capacity.As("Capacity"))
                .Where(o.Id.Eq(command.OfferingId))
                .FirstOrDefaultAsync<CapacityRow>(cancellationToken).ConfigureAwait(false);
            if (offering is null)
            {
                return EnrollmentOutcome.NotFound();
            }

            var taken = await tx
                .From(e)
                .Select(Aggregates.Count(e, "Count"))
                .Where(e.OfferingId.Eq(command.OfferingId))
                .FirstOrDefaultAsync<CountRow>(cancellationToken).ConfigureAwait(false);
            var enrolled = taken?.Count ?? 0;
            if (enrolled >= offering.Capacity)
            {
                return EnrollmentOutcome.Full(offering.Capacity, enrolled);
            }

            var id = await tx.InsertInto(e)
                .Value(e.StudentId, command.StudentId)
                .Value(e.OfferingId, command.OfferingId)
                .Value(e.EnrolledAt, command.EnrolledAt)
                .ExecuteAndReturnIdAsync(cancellationToken).ConfigureAwait(false);

            return EnrollmentOutcome.Success((int)id);
        }, cancellationToken);
    }

    private sealed record IdRow(int Id);
    private sealed record CapacityRow(int Capacity);
    private sealed record CountRow(long Count);
}

public sealed class AssignGradeHandler : ICommandHandler<AssignGradeCommand, bool>
{
    private readonly OrmSession _session;
    public AssignGradeHandler(OrmSession session) => _session = session;

    public async Task<bool> HandleAsync(AssignGradeCommand command, CancellationToken cancellationToken)
    {
        var e = Db.Tables.Enrollments;
        var rows = await _session.Update(e)
            .Set(e.Grade, command.Grade)
            .Where(e.Id.Eq(command.EnrollmentId))
            .ExecuteAsync(cancellationToken).ConfigureAwait(false);
        return rows > 0;
    }
}

public sealed class DropEnrollmentHandler : ICommandHandler<DropEnrollmentCommand, bool>
{
    private readonly OrmSession _session;
    public DropEnrollmentHandler(OrmSession session) => _session = session;

    public async Task<bool> HandleAsync(DropEnrollmentCommand command, CancellationToken cancellationToken)
    {
        var e = Db.Tables.Enrollments;
        var rows = await _session.DeleteFrom(e)
            .Where(e.Id.Eq(command.EnrollmentId))
            .ExecuteAsync(cancellationToken).ConfigureAwait(false);
        return rows > 0;
    }
}
