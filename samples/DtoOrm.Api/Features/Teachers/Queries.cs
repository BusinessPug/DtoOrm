using DtoOrm.Api.Application.Common;

namespace DtoOrm.Api.Features.Teachers;

public sealed record ListTeachersQuery(int? DepartmentId, bool? IsActive, int Take = 100, int Skip = 0)
    : IQuery<IReadOnlyList<TeacherDto>>;

public sealed record GetTeacherByIdQuery(int Id) : IQuery<TeacherDto?>;
