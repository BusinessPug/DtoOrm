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
    private readonly IReadOnlyList<IColumn> _groupBy;
    private readonly SqlCondition? _where;
    private readonly SqlCondition? _having;
    private readonly bool _distinct;
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
        IReadOnlyList<OrderByClause>? orderBy = null,
        IReadOnlyList<IColumn>? groupBy = null,
        SqlCondition? having = null,
        bool distinct = false)
    {
        _session = session;
        _table = table;
        _joins = joins;
        _columns = columns;
        _where = where;
        _take = take;
        _skip = skip;
        _orderBy = orderBy ?? Array.Empty<OrderByClause>();
        _groupBy = groupBy ?? Array.Empty<IColumn>();
        _having = having;
        _distinct = distinct;
    }

    public SelectQueryBuilder Where(SqlCondition condition)
        => With(where: condition ?? throw new ArgumentNullException(nameof(condition)));

    public SelectQueryBuilder Take(int count)
        => With(take: count);

    public SelectQueryBuilder Skip(int count)
        => With(skip: count);

    /// <summary>Emits <c>SELECT DISTINCT</c>, removing duplicate rows from the result.</summary>
    public SelectQueryBuilder Distinct()
        => With(distinct: true);

    public SelectQueryBuilder OrderBy(IColumn column)
        => Append(new OrderByClause(column, SortDirection.Ascending));

    public SelectQueryBuilder OrderByDescending(IColumn column)
        => Append(new OrderByClause(column, SortDirection.Descending));

    /// <summary>
    /// Adds a <c>GROUP BY</c> over one or more columns. Call repeatedly to append further grouping keys.
    /// Combine with <see cref="Aggregates"/> in the projection and <see cref="Having"/> for filtered aggregates.
    /// </summary>
    public SelectQueryBuilder GroupBy(params IColumn[] columns)
    {
        if (columns is null) throw new ArgumentNullException(nameof(columns));
        if (columns.Length == 0)
        {
            throw new InvalidOperationException("GroupBy requires at least one column.");
        }

        var next = new List<IColumn>(_groupBy);
        foreach (var column in columns)
        {
            next.Add(column ?? throw new ArgumentNullException(nameof(columns)));
        }

        return With(groupBy: next);
    }

    /// <summary>
    /// Adds a <c>HAVING</c> clause that filters grouped rows, typically using an aggregate condition
    /// such as <c>Aggregates.Sum(col).Gt(value)</c>. Requires a preceding <see cref="GroupBy"/>.
    /// </summary>
    public SelectQueryBuilder Having(SqlCondition condition)
        => With(having: condition ?? throw new ArgumentNullException(nameof(condition)));

    private SelectQueryBuilder Append(OrderByClause clause)
    {
        if (clause.Column is null) throw new ArgumentNullException(nameof(clause));
        var next = new List<OrderByClause>(_orderBy) { clause };
        return With(orderBy: next);
    }

    private SelectQueryBuilder With(
        SqlCondition? where = null,
        int? take = null,
        int? skip = null,
        IReadOnlyList<OrderByClause>? orderBy = null,
        IReadOnlyList<IColumn>? groupBy = null,
        SqlCondition? having = null,
        bool? distinct = null)
        => new(
            _session,
            _table,
            _joins,
            _columns,
            where ?? _where,
            take ?? _take,
            skip ?? _skip,
            orderBy ?? _orderBy,
            groupBy ?? _groupBy,
            having ?? _having,
            distinct ?? _distinct);

    public QueryCommand ToCommand()
        => _session.Build(_table, _joins, _columns, _where, _take, _skip, _orderBy, _groupBy, _having, _distinct);

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
