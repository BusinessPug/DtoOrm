namespace DtoOrm.Core;

public sealed class InsertBuilder
{
    private readonly OrmSession _session;
    private readonly Table _table;
    private readonly List<(IColumn Column, object? Value)> _assignments = new();

    internal InsertBuilder(OrmSession session, Table table)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _table = table ?? throw new ArgumentNullException(nameof(table));
    }

    public InsertBuilder Value<T>(Column<T> column, T value)
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

    public QueryCommand ToCommand()
    {
        if (_assignments.Count == 0)
        {
            throw new InvalidOperationException("Insert requires at least one column value.");
        }

        var dialect = _session.Dialect;
        var ctx = new SqlRenderContext(dialect, qualifyColumns: false);

        var columns = string.Join(", ", _assignments.Select(a => dialect.QuoteIdentifier(a.Column.DbName)));
        var values = string.Join(", ", _assignments.Select(a => ctx.AddParameter(a.Value)));

        var sql = "INSERT INTO " + _table.RenderUnaliased(dialect) +
                  " (" + columns + ")" + Environment.NewLine +
                  "VALUES (" + values + ")";

        return new QueryCommand(sql, ctx.Parameters);
    }

    public Task<int> ExecuteAsync(CancellationToken cancellationToken = default)
        => _session.ExecuteAsync(ToCommand(), cancellationToken);

    public async Task<long> ExecuteAndReturnIdAsync(CancellationToken cancellationToken = default)
    {
        var command = ToCommand();
        var lastIdSelect = _session.Dialect.LastInsertedIdSelect();
        var combined = new QueryCommand(command.Sql + ";" + Environment.NewLine + lastIdSelect, command.Parameters);
        var result = await _session.ExecuteScalarAsync(combined, cancellationToken).ConfigureAwait(false);
        return Convert.ToInt64(result, System.Globalization.CultureInfo.InvariantCulture);
    }
}
