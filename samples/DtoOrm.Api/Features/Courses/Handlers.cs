using DtoOrm.Api.Generated;
using DtoOrm.Api.Application.Common;
using DtoOrm.Core;

namespace DtoOrm.Api.Features.Courses;

public sealed class ListCoursesHandler : IQueryHandler<ListCoursesQuery, IReadOnlyList<CourseDto>>
{
    private readonly OrmSession _session;
    public ListCoursesHandler(OrmSession session) => _session = session;

    public Task<IReadOnlyList<CourseDto>> HandleAsync(ListCoursesQuery query, CancellationToken cancellationToken)
    {
        var c = Db.Tables.Courses;
        var builder = _session
            .From(c)
            .Select(c.Id, c.Code, c.Title, c.Credits, c.DepartmentId);

        if (query.DepartmentId is not null)
        {
            builder = builder.Where(c.DepartmentId.Eq(query.DepartmentId.Value));
        }

        return builder.Take(query.Take).Skip(query.Skip).ToListAsync<CourseDto>(cancellationToken);
    }
}

public sealed class GetCourseByIdHandler : IQueryHandler<GetCourseByIdQuery, CourseDto?>
{
    private readonly OrmSession _session;
    public GetCourseByIdHandler(OrmSession session) => _session = session;

    public Task<CourseDto?> HandleAsync(GetCourseByIdQuery query, CancellationToken cancellationToken)
    {
        var c = Db.Tables.Courses;
        return _session
            .From(c)
            .Select(c.Id, c.Code, c.Title, c.Credits, c.DepartmentId)
            .Where(c.Id.Eq(query.Id))
            .SingleOrDefaultAsync<CourseDto>(cancellationToken);
    }
}

public sealed class CreateCourseHandler : ICommandHandler<CreateCourseCommand, int>
{
    private readonly OrmSession _session;
    public CreateCourseHandler(OrmSession session) => _session = session;

    public async Task<int> HandleAsync(CreateCourseCommand command, CancellationToken cancellationToken)
    {
        var c = Db.Tables.Courses;
        var id = await _session.InsertInto(c)
            .Value(c.Code, command.Code)
            .Value(c.Title, command.Title)
            .Value(c.Credits, command.Credits)
            .Value(c.DepartmentId, command.DepartmentId)
            .ExecuteAndReturnIdAsync(cancellationToken).ConfigureAwait(false);
        return (int)id;
    }
}

public sealed class UpdateCourseHandler : ICommandHandler<UpdateCourseCommand, bool>
{
    private readonly OrmSession _session;
    public UpdateCourseHandler(OrmSession session) => _session = session;

    public async Task<bool> HandleAsync(UpdateCourseCommand command, CancellationToken cancellationToken)
    {
        var c = Db.Tables.Courses;
        var rows = await _session.Update(c)
            .Set(c.Code, command.Code)
            .Set(c.Title, command.Title)
            .Set(c.Credits, command.Credits)
            .Set(c.DepartmentId, command.DepartmentId)
            .Where(c.Id.Eq(command.Id))
            .ExecuteAsync(cancellationToken).ConfigureAwait(false);
        return rows > 0;
    }
}

public sealed class DeleteCourseHandler : ICommandHandler<DeleteCourseCommand, bool>
{
    private readonly OrmSession _session;
    public DeleteCourseHandler(OrmSession session) => _session = session;

    public async Task<bool> HandleAsync(DeleteCourseCommand command, CancellationToken cancellationToken)
    {
        var c = Db.Tables.Courses;
        var rows = await _session.DeleteFrom(c)
            .Where(c.Id.Eq(command.Id))
            .ExecuteAsync(cancellationToken).ConfigureAwait(false);
        return rows > 0;
    }
}
