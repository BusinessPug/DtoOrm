using DtoOrm.Api.Application.Common;
using DtoOrm.Api.Generated;
using DtoOrm.Core;

namespace DtoOrm.Api.Features.Reports;

/// <summary>
/// Counts the courses owned by each department. Demonstrates a LEFT JOIN (so departments with no
/// courses still appear with a count of zero), a COUNT aggregate and GROUP BY.
/// </summary>
public sealed class DepartmentCatalogHandler
    : IQueryHandler<DepartmentCatalogQuery, IReadOnlyList<DepartmentCatalogDto>>
{
    private readonly OrmSession _session;
    public DepartmentCatalogHandler(OrmSession session) => _session = session;

    public Task<IReadOnlyList<DepartmentCatalogDto>> HandleAsync(
        DepartmentCatalogQuery query, CancellationToken cancellationToken)
    {
        var d = Db.Tables.Departments;
        var c = Db.Tables.Courses;

        return _session
            .From(d)
            .LeftJoin(c, c.DepartmentId.EqColumn(d.Id))
            .Select(
                d.Id.As("DepartmentId"),
                d.Code,
                d.Name,
                Aggregates.Count(c.Id, "CourseCount"))
            .GroupBy(d.Id, d.Code, d.Name)
            .OrderBy(d.Name)
            .ToListAsync<DepartmentCatalogDto>(cancellationToken);
    }
}

/// <summary>
/// Ranks courses by the number of students enrolled across all of their offerings. Demonstrates two
/// INNER JOINs, a COUNT aggregate, GROUP BY, an optional HAVING filter and a descending ORDER BY on
/// the aggregate.
/// </summary>
public sealed class CoursePopularityHandler
    : IQueryHandler<CoursePopularityQuery, IReadOnlyList<CoursePopularityDto>>
{
    private readonly OrmSession _session;
    public CoursePopularityHandler(OrmSession session) => _session = session;

    public Task<IReadOnlyList<CoursePopularityDto>> HandleAsync(
        CoursePopularityQuery query, CancellationToken cancellationToken)
    {
        var e = Db.Tables.Enrollments;
        var o = Db.Tables.Offerings;
        var c = Db.Tables.Courses;

        var enrollments = Aggregates.Count(e.Id, "EnrollmentCount");

        var builder = _session
            .From(e)
            .InnerJoin(o, o.Id.EqColumn(e.OfferingId))
            .InnerJoin(c, c.Id.EqColumn(o.CourseId))
            .Select(c.Id.As("CourseId"), c.Code, c.Title, enrollments)
            .GroupBy(c.Id, c.Code, c.Title);

        if (query.MinEnrollments > 0)
        {
            builder = builder.Having(enrollments.Gte(query.MinEnrollments));
        }

        return builder
            .OrderByDescending(enrollments)
            .OrderBy(c.Code)
            .Take(query.Take)
            .ToListAsync<CoursePopularityDto>(cancellationToken);
    }
}

/// <summary>
/// Reports seat utilisation per offering. Demonstrates three INNER JOINs (course, term, teacher), a
/// LEFT JOIN to enrollments (so empty offerings still show), COUNT, GROUP BY and an optional filter.
/// </summary>
public sealed class OfferingSeatsHandler
    : IQueryHandler<OfferingSeatsQuery, IReadOnlyList<OfferingSeatsDto>>
{
    private readonly OrmSession _session;
    public OfferingSeatsHandler(OrmSession session) => _session = session;

    public Task<IReadOnlyList<OfferingSeatsDto>> HandleAsync(
        OfferingSeatsQuery query, CancellationToken cancellationToken)
    {
        var o = Db.Tables.Offerings;
        var c = Db.Tables.Courses;
        var tm = Db.Tables.Terms;
        var t = Db.Tables.Teachers;
        var e = Db.Tables.Enrollments;

        var builder = _session
            .From(o)
            .InnerJoin(c, c.Id.EqColumn(o.CourseId))
            .InnerJoin(tm, tm.Id.EqColumn(o.TermId))
            .InnerJoin(t, t.Id.EqColumn(o.TeacherId))
            .LeftJoin(e, e.OfferingId.EqColumn(o.Id))
            .Select(
                o.Id.As("OfferingId"),
                c.Code.As("CourseCode"),
                c.Title.As("CourseTitle"),
                tm.Name.As("TermName"),
                t.FirstName.As("TeacherFirstName"),
                t.LastName.As("TeacherLastName"),
                o.Room,
                o.Capacity,
                Aggregates.Count(e.Id, "Enrolled"));

        if (query.TermId is not null)
        {
            builder = builder.Where(o.TermId.Eq(query.TermId.Value));
        }

        return builder
            .GroupBy(o.Id, c.Code, c.Title, tm.Name, t.FirstName, t.LastName, o.Room, o.Capacity)
            .OrderBy(tm.Name)
            .OrderBy(c.Code)
            .ToListAsync<OfferingSeatsDto>(cancellationToken);
    }
}
