using DtoOrm.Api.Application.Common;

namespace DtoOrm.Api.Features.Departments;

public sealed record ListDepartmentsQuery : IQuery<IReadOnlyList<DepartmentDto>>;
public sealed record GetDepartmentByIdQuery(int Id) : IQuery<DepartmentDto?>;
