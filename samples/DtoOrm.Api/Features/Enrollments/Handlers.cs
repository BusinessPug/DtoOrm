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

public sealed class EnrollStudentHandler : ICommandHandler<EnrollStudentCommand, int>
{
    private readonly OrmSession _session;
    public EnrollStudentHandler(OrmSession session) => _session = session;

    public async Task<int> HandleAsync(EnrollStudentCommand command, CancellationToken cancellationToken)
    {
        var e = Db.Tables.Enrollments;
        var id = await _session.InsertInto(e)
            .Value(e.StudentId, command.StudentId)
            .Value(e.OfferingId, command.OfferingId)
            .Value(e.EnrolledAt, command.EnrolledAt)
            .ExecuteAndReturnIdAsync(cancellationToken).ConfigureAwait(false);
        return (int)id;
    }
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
