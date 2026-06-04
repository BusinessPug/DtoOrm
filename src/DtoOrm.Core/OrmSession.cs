using System.Data.Common;

namespace DtoOrm.Core;

public sealed class OrmSession : IAsyncDisposable
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ISqlDialect _dialect;
    private readonly IRowMapper _mapper;
    private readonly List<IAsyncDisposable> _ownedDisposables = new();

    public OrmSession(
        IDbConnectionFactory connectionFactory,
        ISqlDialect dialect,
        IRowMapper? mapper = null,
        IEnumerable<IAsyncDisposable>? ownedDisposables = null)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _dialect = dialect ?? throw new ArgumentNullException(nameof(dialect));
        _mapper = mapper ?? ReflectionRowMapper.Instance;

        if (ownedDisposables is not null)
        {
            _ownedDisposables.AddRange(ownedDisposables);
        }
    }

    internal ISqlDialect Dialect => _dialect;

    public FromQueryBuilder From(Table table) => new(this, table);

    public InsertBuilder InsertInto(Table table) => new(this, table);

    public UpdateBuilder Update(Table table) => new(this, table);

    public DeleteBuilder DeleteFrom(Table table) => new(this, table);

    internal QueryCommand Build(
        Table table,
        IReadOnlyList<JoinClause> joins,
        IReadOnlyList<IColumn> columns,
        SqlCondition? where,
        int? take,
        int? skip,
        IReadOnlyList<OrderByClause>? orderBy = null,
        IReadOnlyList<IColumn>? groupBy = null,
        SqlCondition? having = null,
        bool distinct = false)
    {
        if (columns.Count == 0)
        {
            throw new InvalidOperationException("At least one selected column is required.");
        }

        if (having is not null && (groupBy is null || groupBy.Count == 0))
        {
            throw new InvalidOperationException("HAVING requires a GROUP BY clause. Call GroupBy(...) before Having(...).");
        }

        var ctx = new SqlRenderContext(_dialect, qualifyColumns: true);

        var sql = "SELECT " + (distinct ? "DISTINCT " : string.Empty) +
                  string.Join(", ", columns.Select(c => c.RenderSelect(_dialect))) +
                  Environment.NewLine +
                  "FROM " + table.Render(_dialect);

        foreach (var join in joins)
        {
            sql += Environment.NewLine + join.Render(ctx);
        }

        if (where is not null)
        {
            sql += Environment.NewLine + "WHERE " + where.Render(ctx);
        }

        if (groupBy is { Count: > 0 })
        {
            var keys = groupBy.Select(c => c.Render(_dialect));
            sql += Environment.NewLine + "GROUP BY " + string.Join(", ", keys);
        }

        if (having is not null)
        {
            sql += Environment.NewLine + "HAVING " + having.Render(ctx);
        }

        if (orderBy is { Count: > 0 })
        {
            var parts = orderBy.Select(o =>
                o.Column.Render(_dialect) + (o.Direction == SortDirection.Descending ? " DESC" : " ASC"));
            sql += Environment.NewLine + "ORDER BY " + string.Join(", ", parts);
        }

        if (take is not null)
        {
            if (take < 0) throw new ArgumentOutOfRangeException(nameof(take), "Take cannot be negative.");
            sql += Environment.NewLine + "LIMIT " + take.Value;
        }

        if (skip is not null)
        {
            if (skip < 0) throw new ArgumentOutOfRangeException(nameof(skip), "Skip cannot be negative.");
            sql += Environment.NewLine + "OFFSET " + skip.Value;
        }

        return new QueryCommand(sql, ctx.Parameters);
    }

    internal async Task<IReadOnlyList<TDto>> QueryAsync<TDto>(
        QueryCommand command,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var dbCommand = connection.CreateCommand();

        dbCommand.CommandText = command.Sql;
        BindParameters(dbCommand, command.Parameters);

        var rows = new List<TDto>();

        await using var reader = await dbCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(_mapper.Map<TDto>(reader));
        }

        return rows;
    }

    public async Task<int> ExecuteAsync(QueryCommand command, CancellationToken cancellationToken = default)
    {
        if (command is null) throw new ArgumentNullException(nameof(command));

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var dbCommand = connection.CreateCommand();

        dbCommand.CommandText = command.Sql;
        BindParameters(dbCommand, command.Parameters);

        return await dbCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<object?> ExecuteScalarAsync(QueryCommand command, CancellationToken cancellationToken = default)
    {
        if (command is null) throw new ArgumentNullException(nameof(command));

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var dbCommand = connection.CreateCommand();

        dbCommand.CommandText = command.Sql;
        BindParameters(dbCommand, command.Parameters);

        return await dbCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void BindParameters(DbCommand dbCommand, IReadOnlyList<SqlParameterValue> parameters)
    {
        foreach (var parameter in parameters)
        {
            var dbParameter = dbCommand.CreateParameter();
            dbParameter.ParameterName = parameter.Name;
            dbParameter.Value = parameter.Value ?? DBNull.Value;
            dbCommand.Parameters.Add(dbParameter);
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var disposable in _ownedDisposables)
        {
            await disposable.DisposeAsync().ConfigureAwait(false);
        }
    }
}
