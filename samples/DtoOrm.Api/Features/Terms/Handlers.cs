using DtoOrm.Api.Generated;
using DtoOrm.Api.Application.Common;
using DtoOrm.Core;

namespace DtoOrm.Api.Features.Terms;

public sealed class ListTermsHandler : IQueryHandler<ListTermsQuery, IReadOnlyList<TermDto>>
{
    private readonly OrmSession _session;
    public ListTermsHandler(OrmSession session) => _session = session;

    public Task<IReadOnlyList<TermDto>> HandleAsync(ListTermsQuery query, CancellationToken cancellationToken)
    {
        var tm = Db.Tables.Terms;
        return _session
            .From(tm)
            .Select(tm.Id, tm.Code, tm.Name, tm.StartDate, tm.EndDate)
            .ToListAsync<TermDto>(cancellationToken);
    }
}

public sealed class GetTermByIdHandler : IQueryHandler<GetTermByIdQuery, TermDto?>
{
    private readonly OrmSession _session;
    public GetTermByIdHandler(OrmSession session) => _session = session;

    public Task<TermDto?> HandleAsync(GetTermByIdQuery query, CancellationToken cancellationToken)
    {
        var tm = Db.Tables.Terms;
        return _session
            .From(tm)
            .Select(tm.Id, tm.Code, tm.Name, tm.StartDate, tm.EndDate)
            .Where(tm.Id.Eq(query.Id))
            .SingleOrDefaultAsync<TermDto>(cancellationToken);
    }
}
