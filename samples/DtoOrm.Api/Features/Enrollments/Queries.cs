using DtoOrm.Api.Application.Common;

namespace DtoOrm.Api.Features.Enrollments;

public sealed record ListEnrollmentsQuery(int? StudentId, int? OfferingId, int Take = 200, int Skip = 0)
    : IQuery<IReadOnlyList<EnrollmentDto>>;

public sealed record GetStudentTranscriptQuery(int StudentId) : IQuery<IReadOnlyList<TranscriptEntryDto>>;
