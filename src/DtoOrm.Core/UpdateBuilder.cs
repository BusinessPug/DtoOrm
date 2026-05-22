namespace DtoOrm.Core;

public sealed class UpdateBuilder
{
    private readonly OrmSession _session;
    private readonly Table _table;
    private readonly List<(IColumn Column, object? Value)> _assignments = new();
    private SqlCondition? _where;
    private bool _unfilteredAllowed;

    internal UpdateBuilder(OrmSession session, Table table)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _table = table ?? throw new ArgumentNullException(nameof(table));
    }

    public UpdateBuilder Set<T>(Column<T> column, T value)
    {
        if (column is null) throw new ArgumentNullException(nameof(column));
        if (!ReferenceEquals(column.Table, _table))
        {
            throw new InvalidOperationException(
                $"Column '{column}' does not belong to table '{_table.ClrName}'.");
        }

        _assignments.Add((column, (object?)value));
        return this;
    }

    public UpdateBuilder Where(SqlCondition condition)
    {
        _where = condition ?? throw new ArgumentNullException(nameof(condition));
        return this;
    }

    public UpdateBuilder Unfiltered()
    {
        _unfilteredAllowed = true;
        return this;
    }

    public QueryCommand ToCommand()
    {
        if (_assignments.Count == 0)
        {
            throw new InvalidOperationException("Update requires at least one SET assignment.");
        }

        if (_where is null && !_unfilteredAllowed)
        {
            throw new InvalidOperationException(
                "Update without a WHERE clause is refused. Call Unfiltered() explicitly to allow an unfiltered update.");
        }

        var dialect = _session.Dialect;
        var ctx = new SqlRenderContext(dialect, qualifyColumns: false);

        var setClauses = _assignments.Select(a =>
            dialect.QuoteIdentifier(a.Column.DbName) + " = " + ctx.AddParameter(a.Value));

        var sql = "UPDATE " + _table.RenderUnaliased(dialect) + Environment.NewLine +
                  "SET " + string.Join(", ", setClauses);

        if (_where is not null)
        {
            sql += Environment.NewLine + "WHERE " + _where.Render(ctx);
        }

        return new QueryCommand(sql, ctx.Parameters);
    }

    public Task<int> ExecuteAsync(CancellationToken cancellationToken = default)
        => _session.ExecuteAsync(ToCommand(), cancellationToken);
}
