using DtoOrm.Api.Generated;
using DtoOrm.Api.Application.Common;
using DtoOrm.Core;

namespace DtoOrm.Api.Features.Offerings;

public sealed class ListOfferingsHandler : IQueryHandler<ListOfferingsQuery, IReadOnlyList<OfferingDto>>
{
    private readonly OrmSession _session;
    public ListOfferingsHandler(OrmSession session) => _session = session;

    public Task<IReadOnlyList<OfferingDto>> HandleAsync(ListOfferingsQuery query, CancellationToken cancellationToken)
    {
        var o = Db.Tables.Offerings;
        var builder = _session
            .From(o)
            .Select(o.Id, o.CourseId, o.TeacherId, o.TermId, o.Capacity, o.Room);

        SqlCondition? where = null;
        if (query.CourseId is not null)
        {
            where = o.CourseId.Eq(query.CourseId.Value);
        }
        if (query.TermId is not null)
        {
            var c = o.TermId.Eq(query.TermId.Value);
            where = where is null ? c : where & c;
        }
        if (query.TeacherId is not null)
        {
            var c = o.TeacherId.Eq(query.TeacherId.Value);
            where = where is null ? c : where & c;
        }
        if (where is not null)
        {
            builder = builder.Where(where);
        }

        return builder.Take(query.Take).Skip(query.Skip).ToListAsync<OfferingDto>(cancellationToken);
    }
}

public sealed class GetOfferingByIdHandler : IQueryHandler<GetOfferingByIdQuery, OfferingDto?>
{
    private readonly OrmSession _session;
    public GetOfferingByIdHandler(OrmSession session) => _session = session;

    public Task<OfferingDto?> HandleAsync(GetOfferingByIdQuery query, CancellationToken cancellationToken)
    {
        var o = Db.Tables.Offerings;
        return _session
            .From(o)
            .Select(o.Id, o.CourseId, o.TeacherId, o.TermId, o.Capacity, o.Room)
            .Where(o.Id.Eq(query.Id))
            .SingleOrDefaultAsync<OfferingDto>(cancellationToken);
    }
}

public sealed class GetOfferingDetailsHandler : IQueryHandler<GetOfferingDetailsQuery, OfferingDetailsDto?>
{
    private readonly OrmSession _session;
    public GetOfferingDetailsHandler(OrmSession session) => _session = session;

    public Task<OfferingDetailsDto?> HandleAsync(GetOfferingDetailsQuery query, CancellationToken cancellationToken)
    {
        var o = Db.Tables.Offerings;
        var c = Db.Tables.Courses;
        var t = Db.Tables.Teachers;
        var tm = Db.Tables.Terms;

        return _session
            .From(o)
            .InnerJoin(c, c.Id.EqColumn(o.CourseId))
            .InnerJoin(t, t.Id.EqColumn(o.TeacherId))
            .InnerJoin(tm, tm.Id.EqColumn(o.TermId))
            .Select(
                o.Id.As("Id"),
                c.Id.As("CourseId"),
                c.Code.As("CourseCode"),
                c.Title.As("CourseTitle"),
                c.Credits.As("Credits"),
                t.Id.As("TeacherId"),
                t.FirstName.As("TeacherFirstName"),
                t.LastName.As("TeacherLastName"),
                tm.Id.As("TermId"),
                tm.Name.As("TermName"),
                o.Capacity.As("Capacity"),
                o.Room.As("Room"))
            .Where(o.Id.Eq(query.Id))
            .SingleOrDefaultAsync<OfferingDetailsDto>(cancellationToken);
    }
}

public sealed class GetOfferingRosterHandler : IQueryHandler<GetOfferingRosterQuery, IReadOnlyList<RosterEntryDto>>
{
    private readonly OrmSession _session;
    public GetOfferingRosterHandler(OrmSession session) => _session = session;

    public Task<IReadOnlyList<RosterEntryDto>> HandleAsync(GetOfferingRosterQuery query, CancellationToken cancellationToken)
    {
        var e = Db.Tables.Enrollments;
        var s = Db.Tables.Students;

        return _session
            .From(e)
            .InnerJoin(s, s.Id.EqColumn(e.StudentId))
            .Select(
                e.Id.As("EnrollmentId"),
                s.Id.As("StudentId"),
                s.FirstName.As("FirstName"),
                s.LastName.As("LastName"),
                s.Email.As("Email"),
                e.EnrolledAt.As("EnrolledAt"),
                e.Grade.As("Grade"))
            .Where(e.OfferingId.Eq(query.OfferingId))
            .OrderBy(s.LastName)
            .OrderBy(s.FirstName)
            .ToListAsync<RosterEntryDto>(cancellationToken);
    }
}

public sealed class CreateOfferingHandler : ICommandHandler<CreateOfferingCommand, int>
{
    private readonly OrmSession _session;
    public CreateOfferingHandler(OrmSession session) => _session = session;

    public async Task<int> HandleAsync(CreateOfferingCommand command, CancellationToken cancellationToken)
    {
        var o = Db.Tables.Offerings;
        var id = await _session.InsertInto(o)
            .Value(o.CourseId, command.CourseId)
            .Value(o.TeacherId, command.TeacherId)
            .Value(o.TermId, command.TermId)
            .Value(o.Capacity, command.Capacity)
            .Value(o.Room, command.Room)
            .ExecuteAndReturnIdAsync(cancellationToken).ConfigureAwait(false);
        return (int)id;
    }
}

public sealed class UpdateOfferingHandler : ICommandHandler<UpdateOfferingCommand, bool>
{
    private readonly OrmSession _session;
    public UpdateOfferingHandler(OrmSession session) => _session = session;

    public async Task<bool> HandleAsync(UpdateOfferingCommand command, CancellationToken cancellationToken)
    {
        var o = Db.Tables.Offerings;
        var rows = await _session.Update(o)
            .Set(o.CourseId, command.CourseId)
            .Set(o.TeacherId, command.TeacherId)
            .Set(o.TermId, command.TermId)
            .Set(o.Capacity, command.Capacity)
            .Set(o.Room, command.Room)
            .Where(o.Id.Eq(command.Id))
            .ExecuteAsync(cancellationToken).ConfigureAwait(false);
        return rows > 0;
    }
}

public sealed class DeleteOfferingHandler : ICommandHandler<DeleteOfferingCommand, bool>
{
    private readonly OrmSession _session;
    public DeleteOfferingHandler(OrmSession session) => _session = session;

    public async Task<bool> HandleAsync(DeleteOfferingCommand command, CancellationToken cancellationToken)
    {
        var o = Db.Tables.Offerings;
        var rows = await _session.DeleteFrom(o)
            .Where(o.Id.Eq(command.Id))
            .ExecuteAsync(cancellationToken).ConfigureAwait(false);
        return rows > 0;
    }
}
