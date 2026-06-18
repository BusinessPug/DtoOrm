using DtoOrm.Api.Application.Common;
using DtoOrm.Api.Generated;
using DtoOrm.Core;

namespace DtoOrm.Api.Features.Schedule;

public sealed class ScheduleHandler : IQueryHandler<ScheduleQuery, IReadOnlyList<ScheduleItemDto>>
{
    private readonly OrmSession _session;

    public ScheduleHandler(OrmSession session) => _session = session;

    public Task<IReadOnlyList<ScheduleItemDto>> HandleAsync(ScheduleQuery query, CancellationToken cancellationToken)
    {
        var cm = Db.Tables.CourseMeetings;
        var o = Db.Tables.Offerings;
        var c = Db.Tables.Courses;
        var tm = Db.Tables.Terms;
        var t = Db.Tables.Teachers;

        var from = _session
            .From(cm)
            .InnerJoin(o, o.Id.EqColumn(cm.OfferingId))
            .InnerJoin(c, c.Id.EqColumn(o.CourseId))
            .InnerJoin(tm, tm.Id.EqColumn(o.TermId))
            .InnerJoin(t, t.Id.EqColumn(o.TeacherId));

        SqlCondition? where = null;
        if (query.StudentId is not null)
        {
            var e = Db.Tables.Enrollments;
            from = from.InnerJoin(e, e.OfferingId.EqColumn(o.Id));
            where = e.StudentId.Eq(query.StudentId.Value);
        }

        var builder = from
            .Select(
                cm.Id.As("MeetingId"),
                o.Id.As("OfferingId"),
                c.Id.As("CourseId"),
                c.Code.As("CourseCode"),
                c.Title.As("CourseTitle"),
                tm.Id.As("TermId"),
                tm.Name.As("TermName"),
                t.Id.As("TeacherId"),
                t.FirstName.As("TeacherFirstName"),
                t.LastName.As("TeacherLastName"),
                cm.DayOfWeek.As("DayOfWeek"),
                cm.StartsAt.As("StartsAt"),
                cm.EndsAt.As("EndsAt"),
                cm.Location.As("Location"));

        if (query.TeacherId is not null)
        {
            var teacher = o.TeacherId.Eq(query.TeacherId.Value);
            where = where is null ? teacher : where & teacher;
        }
        if (query.TermId is not null)
        {
            var term = o.TermId.Eq(query.TermId.Value);
            where = where is null ? term : where & term;
        }
        if (where is not null)
        {
            builder = builder.Where(where);
        }

        return builder
            .OrderBy(cm.DayOfWeek)
            .OrderBy(cm.StartsAt)
            .OrderBy(c.Code)
            .ToListAsync<ScheduleItemDto>(cancellationToken);
    }
}

public sealed class AcademicBreaksHandler : IQueryHandler<AcademicBreaksQuery, IReadOnlyList<AcademicBreakDto>>
{
    private readonly OrmSession _session;

    public AcademicBreaksHandler(OrmSession session) => _session = session;

    public Task<IReadOnlyList<AcademicBreakDto>> HandleAsync(AcademicBreaksQuery query, CancellationToken cancellationToken)
    {
        var b = Db.Tables.AcademicBreaks;
        var tm = Db.Tables.Terms;

        var builder = _session
            .From(b)
            .InnerJoin(tm, tm.Id.EqColumn(b.TermId))
            .Select(
                b.Id,
                b.TermId,
                tm.Name.As("TermName"),
                b.Name,
                b.StartDate,
                b.EndDate,
                b.Notes);

        if (query.TermId is not null)
        {
            builder = builder.Where(b.TermId.Eq(query.TermId.Value));
        }

        return builder
            .OrderBy(b.StartDate)
            .ToListAsync<AcademicBreakDto>(cancellationToken);
    }
}
