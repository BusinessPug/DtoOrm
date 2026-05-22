using DtoOrm.Api.Application.Common;

namespace DtoOrm.Api.Features.Students;

public sealed record ListStudentsQuery(string? LastNameLike, bool? IsActive, int Take = 50, int Skip = 0)
    : IQuery<IReadOnlyList<StudentDto>>;

public sealed record GetStudentByIdQuery(int Id) : IQuery<StudentDto?>;

public sealed record GetStudentByEmailQuery(string Email) : IQuery<StudentDto?>;