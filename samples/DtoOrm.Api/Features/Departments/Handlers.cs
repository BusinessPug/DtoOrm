using DtoOrm.Api.Generated;
using DtoOrm.Api.Application.Common;
using DtoOrm.Core;

namespace DtoOrm.Api.Features.Departments;

public sealed class ListDepartmentsHandler : IQueryHandler<ListDepartmentsQuery, IReadOnlyList<DepartmentDto>>
{
    private readonly OrmSession _session;
    public ListDepartmentsHandler(OrmSession session) => _session = session;

    public Task<IReadOnlyList<DepartmentDto>> HandleAsync(ListDepartmentsQuery query, CancellationToken cancellationToken)
    {
        var d = Db.Tables.Departments;
        return _session
            .From(d)
            .Select(d.Id, d.Code, d.Name)
            .ToListAsync<DepartmentDto>(cancellationToken);
    }
}

public sealed class GetDepartmentByIdHandler : IQueryHandler<GetDepartmentByIdQuery, DepartmentDto?>
{
    private readonly OrmSession _session;
    public GetDepartmentByIdHandler(OrmSession session) => _session = session;

    public Task<DepartmentDto?> HandleAsync(GetDepartmentByIdQuery query, CancellationToken cancellationToken)
    {
        var d = Db.Tables.Departments;
        return _session
            .From(d)
            .Select(d.Id, d.Code, d.Name)
            .Where(d.Id.Eq(query.Id))
            .SingleOrDefaultAsync<DepartmentDto>(cancellationToken);
    }
}

public sealed class CreateDepartmentHandler : ICommandHandler<CreateDepartmentCommand, int>
{
    private readonly OrmSession _session;
    public CreateDepartmentHandler(OrmSession session) => _session = session;

    public async Task<int> HandleAsync(CreateDepartmentCommand command, CancellationToken cancellationToken)
    {
        var d = Db.Tables.Departments;
        var id = await _session.InsertInto(d)
            .Value(d.Code, command.Code)
            .Value(d.Name, command.Name)
            .ExecuteAndReturnIdAsync(cancellationToken).ConfigureAwait(false);
        return (int)id;
    }
}

public sealed class UpdateDepartmentHandler : ICommandHandler<UpdateDepartmentCommand, bool>
{
    private readonly OrmSession _session;
    public UpdateDepartmentHandler(OrmSession session) => _session = session;

    public async Task<bool> HandleAsync(UpdateDepartmentCommand command, CancellationToken cancellationToken)
    {
        var d = Db.Tables.Departments;
        var rows = await _session.Update(d)
            .Set(d.Code, command.Code)
            .Set(d.Name, command.Name)
            .Where(d.Id.Eq(command.Id))
            .ExecuteAsync(cancellationToken).ConfigureAwait(false);
        return rows > 0;
    }
}

public sealed class DeleteDepartmentHandler : ICommandHandler<DeleteDepartmentCommand, bool>
{
    private readonly OrmSession _session;
    public DeleteDepartmentHandler(OrmSession session) => _session = session;

    public async Task<bool> HandleAsync(DeleteDepartmentCommand command, CancellationToken cancellationToken)
    {
        var d = Db.Tables.Departments;
        var rows = await _session.DeleteFrom(d)
            .Where(d.Id.Eq(command.Id))
            .ExecuteAsync(cancellationToken).ConfigureAwait(false);
        return rows > 0;
    }
}
