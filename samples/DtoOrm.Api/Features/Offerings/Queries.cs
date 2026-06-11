using DtoOrm.Api.Application.Common;

namespace DtoOrm.Api.Features.Offerings;

public sealed record ListOfferingsQuery(int? CourseId, int? TermId, int? TeacherId, int Take = 100, int Skip = 0)
    : IQuery<IReadOnlyList<OfferingDto>>;

public sealed record GetOfferingByIdQuery(int Id) : IQuery<OfferingDto?>;

public sealed record GetOfferingDetailsQuery(int Id) : IQuery<OfferingDetailsDto?>;

public sealed record GetOfferingRosterQuery(int OfferingId) : IQuery<IReadOnlyList<RosterEntryDto>>;
