using DtoOrm.Api.Application.Common;

namespace DtoOrm.Api.Features.Departments;

public sealed record CreateDepartmentCommand(string Code, string Name) : ICommand<int>;
public sealed record UpdateDepartmentCommand(int Id, string Code, string Name) : ICommand<bool>;
public sealed record DeleteDepartmentCommand(int Id) : ICommand<bool>;
