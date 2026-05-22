using DtoOrm.Api.Application.Common;

namespace DtoOrm.Api.Features.Terms;

public sealed record ListTermsQuery : IQuery<IReadOnlyList<TermDto>>;
public sealed record GetTermByIdQuery(int Id) : IQuery<TermDto?>;
