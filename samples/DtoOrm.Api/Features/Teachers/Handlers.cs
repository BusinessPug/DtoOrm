using DtoOrm.Api.Generated;
using DtoOrm.Api.Application.Common;
using DtoOrm.Core;

namespace DtoOrm.Api.Features.Teachers;

public sealed class ListTeachersHandler : IQueryHandler<ListTeachersQuery, IReadOnlyList<TeacherDto>>
{
    private readonly OrmSession _session;
    public ListTeachersHandler(OrmSession session) => _session = session;

    public Task<IReadOnlyList<TeacherDto>> HandleAsync(ListTeachersQuery query, CancellationToken cancellationToken)
    {
        var t = Db.Tables.Teachers;
        var builder = _session
            .From(t)
            .Select(t.Id, t.FirstName, t.LastName, t.Email, t.DepartmentId, t.HiredAt, t.IsActive);

        SqlCondition? where = null;
        if (query.DepartmentId is not null)
        {
            where = t.DepartmentId.Eq(query.DepartmentId.Value);
        }
        if (query.IsActive is not null)
        {
            var active = t.IsActive.Eq(query.IsActive.Value);
            where = where is null ? active : where & active;
        }
        if (where is not null)
        {
            builder = builder.Where(where);
        }

        return builder.Take(query.Take).Skip(query.Skip).ToListAsync<TeacherDto>(cancellationToken);
    }
}

public sealed class GetTeacherByIdHandler : IQueryHandler<GetTeacherByIdQuery, TeacherDto?>
{
    private readonly OrmSession _session;
    public GetTeacherByIdHandler(OrmSession session) => _session = session;

    public Task<TeacherDto?> HandleAsync(GetTeacherByIdQuery query, CancellationToken cancellationToken)
    {
        var t = Db.Tables.Teachers;
        return _session
            .From(t)
            .Select(t.Id, t.FirstName, t.LastName, t.Email, t.DepartmentId, t.HiredAt, t.IsActive)
            .Where(t.Id.Eq(query.Id))
            .SingleOrDefaultAsync<TeacherDto>(cancellationToken);
    }
}

public sealed class CreateTeacherHandler : ICommandHandler<CreateTeacherCommand, int>
{
    private readonly OrmSession _session;
    public CreateTeacherHandler(OrmSession session) => _session = session;

    public async Task<int> HandleAsync(CreateTeacherCommand command, CancellationToken cancellationToken)
    {
        var t = Db.Tables.Teachers;
        var id = await _session.InsertInto(t)
            .Value(t.FirstName, command.FirstName)
            .Value(t.LastName, command.LastName)
            .Value(t.Email, command.Email)
            .Value(t.DepartmentId, command.DepartmentId)
            .Value(t.HiredAt, command.HiredAt)
            .ExecuteAndReturnIdAsync(cancellationToken).ConfigureAwait(false);
        return (int)id;
    }
}

public sealed class UpdateTeacherHandler : ICommandHandler<UpdateTeacherCommand, bool>
{
    private readonly OrmSession _session;
    public UpdateTeacherHandler(OrmSession session) => _session = session;

    public async Task<bool> HandleAsync(UpdateTeacherCommand command, CancellationToken cancellationToken)
    {
        var t = Db.Tables.Teachers;
        var rows = await _session.Update(t)
            .Set(t.FirstName, command.FirstName)
            .Set(t.LastName, command.LastName)
            .Set(t.Email, command.Email)
            .Set(t.DepartmentId, command.DepartmentId)
            .Set(t.HiredAt, command.HiredAt)
            .Set(t.IsActive, command.IsActive)
            .Where(t.Id.Eq(command.Id))
            .ExecuteAsync(cancellationToken).ConfigureAwait(false);
        return rows > 0;
    }
}

public sealed class DeleteTeacherHandler : ICommandHandler<DeleteTeacherCommand, bool>
{
    private readonly OrmSession _session;
    public DeleteTeacherHandler(OrmSession session) => _session = session;

    public async Task<bool> HandleAsync(DeleteTeacherCommand command, CancellationToken cancellationToken)
    {
        var t = Db.Tables.Teachers;
        var rows = await _session.DeleteFrom(t)
            .Where(t.Id.Eq(command.Id))
            .ExecuteAsync(cancellationToken).ConfigureAwait(false);
        return rows > 0;
    }
}
