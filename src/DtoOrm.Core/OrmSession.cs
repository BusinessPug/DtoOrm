using System.Data;
using System.Data.Common;

namespace DtoOrm.Core;

public sealed class OrmSession : IAsyncDisposable
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ISqlDialect _dialect;
    private readonly IRowMapper _mapper;
    private readonly List<IAsyncDisposable> _ownedDisposables = new();
    private (DbConnection Connection, DbTransaction Transaction)? _ambient;

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

    /// <summary>
    /// Opens a connection and starts a database transaction. All operations issued through the
    /// returned <see cref="OrmTransaction"/> (or through this session while the transaction is
    /// active) run on the same connection and are committed or rolled back together.
    /// </summary>
    public Task<OrmTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        => BeginTransactionInternalAsync(null, cancellationToken);

    /// <summary>
    /// Opens a connection and starts a database transaction using the specified isolation level.
    /// </summary>
    public Task<OrmTransaction> BeginTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken = default)
        => BeginTransactionInternalAsync(isolationLevel, cancellationToken);

    /// <summary>
    /// Runs <paramref name="work"/> inside a transaction, committing when it completes successfully
    /// and rolling back if it throws.
    /// </summary>
    public async Task WithTransactionAsync(Func<OrmTransaction, Task> work, CancellationToken cancellationToken = default)
    {
        if (work is null) throw new ArgumentNullException(nameof(work));

        await using var transaction = await BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await work(transaction).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs <paramref name="work"/> inside a transaction and returns its result, committing when it
    /// completes successfully and rolling back if it throws.
    /// </summary>
    public async Task<TResult> WithTransactionAsync<TResult>(Func<OrmTransaction, Task<TResult>> work, CancellationToken cancellationToken = default)
    {
        if (work is null) throw new ArgumentNullException(nameof(work));

        await using var transaction = await BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var result = await work(transaction).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    private async Task<OrmTransaction> BeginTransactionInternalAsync(IsolationLevel? isolationLevel, CancellationToken cancellationToken)
    {
        if (_ambient is not null)
        {
            throw new InvalidOperationException(
                "A transaction is already active on this session. Commit, roll back, or dispose it before starting another.");
        }

        var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var transaction = isolationLevel is { } level
                ? await connection.BeginTransactionAsync(level, cancellationToken).ConfigureAwait(false)
                : await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            _ambient = (connection, transaction);
            return new OrmTransaction(this, connection, transaction);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    internal void ClearAmbient() => _ambient = null;

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
        var (connection, owned) = await AcquireConnectionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var dbCommand = connection.CreateCommand();

            dbCommand.CommandText = command.Sql;
            if (_ambient is { } ambient) dbCommand.Transaction = ambient.Transaction;
            BindParameters(dbCommand, command.Parameters);

            var rows = new List<TDto>();

            await using var reader = await dbCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(_mapper.Map<TDto>(reader));
            }

            return rows;
        }
        finally
        {
            if (owned) await connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    public async Task<int> ExecuteAsync(QueryCommand command, CancellationToken cancellationToken = default)
    {
        if (command is null) throw new ArgumentNullException(nameof(command));

        var (connection, owned) = await AcquireConnectionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var dbCommand = connection.CreateCommand();

            dbCommand.CommandText = command.Sql;
            if (_ambient is { } ambient) dbCommand.Transaction = ambient.Transaction;
            BindParameters(dbCommand, command.Parameters);

            return await dbCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (owned) await connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    public async Task<object?> ExecuteScalarAsync(QueryCommand command, CancellationToken cancellationToken = default)
    {
        if (command is null) throw new ArgumentNullException(nameof(command));

        var (connection, owned) = await AcquireConnectionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var dbCommand = connection.CreateCommand();

            dbCommand.CommandText = command.Sql;
            if (_ambient is { } ambient) dbCommand.Transaction = ambient.Transaction;
            BindParameters(dbCommand, command.Parameters);

            return await dbCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (owned) await connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async ValueTask<(DbConnection Connection, bool Owned)> AcquireConnectionAsync(CancellationToken cancellationToken)
    {
        if (_ambient is { } ambient)
        {
            return (ambient.Connection, false);
        }

        var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return (connection, true);
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
        if (_ambient is { } ambient)
        {
            _ambient = null;
            try
            {
                await ambient.Transaction.DisposeAsync().ConfigureAwait(false);
            }
            finally
            {
                await ambient.Connection.DisposeAsync().ConfigureAwait(false);
            }
        }

        foreach (var disposable in _ownedDisposables)
        {
            await disposable.DisposeAsync().ConfigureAwait(false);
        }
    }
}
