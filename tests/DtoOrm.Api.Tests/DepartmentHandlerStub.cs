using DtoOrm.Api.Application.Common;
using DtoOrm.Api.Features.Departments;

namespace DtoOrm.Api.Tests;

public sealed class DepartmentHandlerStub :
    IQueryHandler<ListDepartmentsQuery, IReadOnlyList<DepartmentDto>>,
    IQueryHandler<GetDepartmentByIdQuery, DepartmentDto?>,
    ICommandHandler<CreateDepartmentCommand, int>,
    ICommandHandler<UpdateDepartmentCommand, bool>,
    ICommandHandler<DeleteDepartmentCommand, bool>
{
    private readonly List<DepartmentDto> _departments =
    [
        new(1, "CS", "Computer Science"),
        new(2, "MATH", "Mathematics")
    ];

    public CreateDepartmentCommand? LastCreate { get; private set; }
    public UpdateDepartmentCommand? LastUpdate { get; private set; }
    public DeleteDepartmentCommand? LastDelete { get; private set; }
    public int CreatedId { get; set; } = 42;
    public bool UpdateResult { get; set; } = true;
    public bool DeleteResult { get; set; } = true;

    public void Reset()
    {
        LastCreate = null;
        LastUpdate = null;
        LastDelete = null;
        CreatedId = 42;
        UpdateResult = true;
        DeleteResult = true;
    }

    public Task<IReadOnlyList<DepartmentDto>> HandleAsync(ListDepartmentsQuery query, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<DepartmentDto>>(_departments);

    public Task<DepartmentDto?> HandleAsync(GetDepartmentByIdQuery query, CancellationToken cancellationToken)
        => Task.FromResult(_departments.FirstOrDefault(department => department.Id == query.Id));

    public Task<int> HandleAsync(CreateDepartmentCommand command, CancellationToken cancellationToken)
    {
        LastCreate = command;
        return Task.FromResult(CreatedId);
    }

    public Task<bool> HandleAsync(UpdateDepartmentCommand command, CancellationToken cancellationToken)
    {
        LastUpdate = command;
        return Task.FromResult(UpdateResult);
    }

    public Task<bool> HandleAsync(DeleteDepartmentCommand command, CancellationToken cancellationToken)
    {
        LastDelete = command;
        return Task.FromResult(DeleteResult);
    }
}
