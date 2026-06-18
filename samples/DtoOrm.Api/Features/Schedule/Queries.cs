using DtoOrm.Api.Application.Common;

namespace DtoOrm.Api.Features.Schedule;

public sealed record ScheduleQuery(int? StudentId = null, int? TeacherId = null, int? TermId = null)
    : IQuery<IReadOnlyList<ScheduleItemDto>>;

public sealed record AcademicBreaksQuery(int? TermId = null)
    : IQuery<IReadOnlyList<AcademicBreakDto>>;
