using DtoOrm.Api.Application.Common;

namespace DtoOrm.Api.Features.Offerings;

public sealed record ListOfferingsQuery(int? CourseId, int? TermId, int? TeacherId, int Take = 100, int Skip = 0)
    : IQuery<IReadOnlyList<OfferingDto>>;

public sealed record GetOfferingByIdQuery(int Id) : IQuery<OfferingDto?>;
