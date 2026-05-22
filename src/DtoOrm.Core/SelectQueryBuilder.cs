namespace DtoOrm.Core;

public enum SortDirection
{
    Ascending,
    Descending
}

public sealed record OrderByClause(IColumn Column, SortDirection Direction);

public sealed class SelectQueryBuilder
{
    private readonly OrmSession _session;
    private readonly Table _table;
    private readonly IReadOnlyList<JoinClause> _joins;
    private readonly IReadOnlyList<IColumn> _columns;
    private readonly IReadOnlyList<OrderByClause> _orderBy;
    private readonly SqlCondition? _where;
    private readonly int? _take;
    private readonly int? _skip;

    internal SelectQueryBuilder(
        OrmSession session,
        Table table,
        IReadOnlyList<JoinClause> joins,
        IReadOnlyList<IColumn> columns,
        SqlCondition? where = null,
        int? take = null,
        int? skip = null,
        IReadOnlyList<OrderByClause>? orderBy = null)
    {
        _session = session;
        _table = table;
        _joins = joins;
        _columns = columns;
        _where = where;
        _take = take;
        _skip = skip;
        _orderBy = orderBy ?? Array.Empty<OrderByClause>();
    }

    public SelectQueryBuilder Where(SqlCondition condition)
        => new(_session, _table, _joins, _columns, condition, _take, _skip, _orderBy);

    public SelectQueryBuilder Take(int count)
        => new(_session, _table, _joins, _columns, _where, count, _skip, _orderBy);

    public SelectQueryBuilder Skip(int count)
        => new(_session, _table, _joins, _columns, _where, _take, count, _orderBy);

    public SelectQueryBuilder OrderBy(IColumn column)
        => Append(new OrderByClause(column, SortDirection.Ascending));

    public SelectQueryBuilder OrderByDescending(IColumn column)
        => Append(new OrderByClause(column, SortDirection.Descending));

    private SelectQueryBuilder Append(OrderByClause clause)
    {
        if (clause.Column is null) throw new ArgumentNullException(nameof(clause));
        var next = new List<OrderByClause>(_orderBy) { clause };
        return new SelectQueryBuilder(_session, _table, _joins, _columns, _where, _take, _skip, next);
    }

    public QueryCommand ToCommand()
        => _session.Build(_table, _joins, _columns, _where, _take, _skip, _orderBy);

    public Task<IReadOnlyList<TDto>> ToListAsync<TDto>(CancellationToken cancellationToken = default)
        => _session.QueryAsync<TDto>(ToCommand(), cancellationToken);

    public async Task<TDto?> FirstOrDefaultAsync<TDto>(CancellationToken cancellationToken = default)
    {
        var rows = await Take(1).ToListAsync<TDto>(cancellationToken).ConfigureAwait(false);
        return rows.Count == 0 ? default : rows[0];
    }

    public async Task<TDto?> SingleOrDefaultAsync<TDto>(CancellationToken cancellationToken = default)
    {
        var rows = await Take(2).ToListAsync<TDto>(cancellationToken).ConfigureAwait(false);

        return rows.Count switch
        {
            0 => default,
            1 => rows[0],
            _ => throw new InvalidOperationException("Expected zero or one row, but query returned multiple rows.")
        };
    }
}
