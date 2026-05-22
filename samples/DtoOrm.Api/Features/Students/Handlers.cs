using DtoOrm.Api.Generated;
using DtoOrm.Api.Application.Common;
using DtoOrm.Core;

namespace DtoOrm.Api.Features.Students;

public sealed class ListStudentsHandler : IQueryHandler<ListStudentsQuery, IReadOnlyList<StudentDto>>
{
    private readonly OrmSession _session;
    public ListStudentsHandler(OrmSession session) => _session = session;

    public Task<IReadOnlyList<StudentDto>> HandleAsync(ListStudentsQuery query, CancellationToken cancellationToken)
    {
        var s = Db.Tables.Students;
        var builder = _session
            .From(s)
            .Select(s.Id, s.FirstName, s.LastName, s.Email, s.DateOfBirth, s.EnrolledAt, s.IsActive);

        SqlCondition? where = null;
        if (!string.IsNullOrWhiteSpace(query.LastNameLike))
        {
            where = s.LastName.Like(query.LastNameLike + "%");
        }
        if (query.IsActive is not null)
        {
            var active = s.IsActive.Eq(query.IsActive.Value);
            where = where is null ? active : where & active;
        }
        if (where is not null)
        {
            builder = builder.Where(where);
        }

        return builder.Take(query.Take).Skip(query.Skip).ToListAsync<StudentDto>(cancellationToken);
    }
}

public sealed class GetStudentByEmailHandler : IQueryHandler<GetStudentByEmailQuery, StudentDto?>
{
    private readonly OrmSession _session;
    public GetStudentByEmailHandler(OrmSession session) => _session = session;
    public Task<StudentDto?> HandleAsync(GetStudentByEmailQuery query, CancellationToken cancellationToken)
    {
        var s = Db.Tables.Students;
        return _session
            .From(s)
            .Select(s.Id, s.FirstName, s.LastName, s.Email, s.DateOfBirth, s.EnrolledAt, s.IsActive)
            .Where(s.Email.Eq(query.Email))
            .SingleOrDefaultAsync<StudentDto>(cancellationToken);
    }
}

public sealed class GetStudentByIdHandler : IQueryHandler<GetStudentByIdQuery, StudentDto?>
{
    private readonly OrmSession _session;
    public GetStudentByIdHandler(OrmSession session) => _session = session;

    public Task<StudentDto?> HandleAsync(GetStudentByIdQuery query, CancellationToken cancellationToken)
    {
        var s = Db.Tables.Students;
        return _session
            .From(s)
            .Select(s.Id, s.FirstName, s.LastName, s.Email, s.DateOfBirth, s.EnrolledAt, s.IsActive)
            .Where(s.Id.Eq(query.Id))
            .SingleOrDefaultAsync<StudentDto>(cancellationToken);
    }
}

public sealed class CreateStudentHandler : ICommandHandler<CreateStudentCommand, int>
{
    private readonly OrmSession _session;
    public CreateStudentHandler(OrmSession session) => _session = session;

    public async Task<int> HandleAsync(CreateStudentCommand command, CancellationToken cancellationToken)
    {
        var s = Db.Tables.Students;
        var id = await _session.InsertInto(s)
            .Value(s.FirstName, command.FirstName)
            .Value(s.LastName, command.LastName)
            .Value(s.Email, command.Email)
            .Value(s.DateOfBirth, command.DateOfBirth)
            .Value(s.EnrolledAt, command.EnrolledAt)
            .ExecuteAndReturnIdAsync(cancellationToken).ConfigureAwait(false);
        return (int)id;
    }
}

public sealed class UpdateStudentHandler : ICommandHandler<UpdateStudentCommand, bool>
{
    private readonly OrmSession _session;
    public UpdateStudentHandler(OrmSession session) => _session = session;

    public async Task<bool> HandleAsync(UpdateStudentCommand command, CancellationToken cancellationToken)
    {
        var s = Db.Tables.Students;
        var rows = await _session.Update(s)
            .Set(s.FirstName, command.FirstName)
            .Set(s.LastName, command.LastName)
            .Set(s.Email, command.Email)
            .Set(s.DateOfBirth, command.DateOfBirth)
            .Set(s.EnrolledAt, command.EnrolledAt)
            .Set(s.IsActive, command.IsActive)
            .Where(s.Id.Eq(command.Id))
            .ExecuteAsync(cancellationToken).ConfigureAwait(false);
        return rows > 0;
    }
}

public sealed class DeleteStudentHandler : ICommandHandler<DeleteStudentCommand, bool>
{
    private readonly OrmSession _session;
    public DeleteStudentHandler(OrmSession session) => _session = session;

    public async Task<bool> HandleAsync(DeleteStudentCommand command, CancellationToken cancellationToken)
    {
        var s = Db.Tables.Students;
        var rows = await _session.DeleteFrom(s)
            .Where(s.Id.Eq(command.Id))
            .ExecuteAsync(cancellationToken).ConfigureAwait(false);
        return rows > 0;
    }
}
