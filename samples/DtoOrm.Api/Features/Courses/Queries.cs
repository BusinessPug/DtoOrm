using DtoOrm.Api.Application.Common;

namespace DtoOrm.Api.Features.Courses;

public sealed record ListCoursesQuery(int? DepartmentId, int Take = 100, int Skip = 0)
    : IQuery<IReadOnlyList<CourseDto>>;

public sealed record GetCourseByIdQuery(int Id) : IQuery<CourseDto?>;
