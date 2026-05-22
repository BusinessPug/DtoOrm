namespace DtoOrm.Core;

public sealed class DeleteBuilder
{
    private readonly OrmSession _session;
    private readonly Table _table;
    private SqlCondition? _where;
    private bool _unfilteredAllowed;

    internal DeleteBuilder(OrmSession session, Table table)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _table = table ?? throw new ArgumentNullException(nameof(table));
    }

    public DeleteBuilder Where(SqlCondition condition)
    {
        _where = condition ?? throw new ArgumentNullException(nameof(condition));
        return this;
    }

    public DeleteBuilder Unfiltered()
    {
        _unfilteredAllowed = true;
        return this;
    }

    public QueryCommand ToCommand()
    {
        if (_where is null && !_unfilteredAllowed)
        {
            throw new InvalidOperationException(
                "Delete without a WHERE clause is refused. Call Unfiltered() explicitly to allow an unfiltered delete.");
        }

        var dialect = _session.Dialect;
        var ctx = new SqlRenderContext(dialect, qualifyColumns: false);

        var sql = "DELETE FROM " + _table.RenderUnaliased(dialect);

        if (_where is not null)
        {
            sql += Environment.NewLine + "WHERE " + _where.Render(ctx);
        }

        return new QueryCommand(sql, ctx.Parameters);
    }

    public Task<int> ExecuteAsync(CancellationToken cancellationToken = default)
        => _session.ExecuteAsync(ToCommand(), cancellationToken);
}
