namespace DtoOrm.Core;

public sealed class FromQueryBuilder
{
    private readonly OrmSession _session;
    private readonly Table _table;
    private readonly List<JoinClause> _joins;

    internal FromQueryBuilder(OrmSession session, Table table)
        : this(session, table, new List<JoinClause>())
    {
    }

    private FromQueryBuilder(OrmSession session, Table table, List<JoinClause> joins)
    {
        _session = session;
        _table = table ?? throw new ArgumentNullException(nameof(table));
        _joins = joins;
    }

    public FromQueryBuilder InnerJoin(Table table, SqlCondition on) => AddJoin(JoinType.Inner, table, on);
    public FromQueryBuilder Join(Table table, SqlCondition on) => AddJoin(JoinType.Inner, table, on);
    public FromQueryBuilder LeftJoin(Table table, SqlCondition on) => AddJoin(JoinType.Left, table, on);
    public FromQueryBuilder RightJoin(Table table, SqlCondition on) => AddJoin(JoinType.Right, table, on);
    public FromQueryBuilder FullJoin(Table table, SqlCondition on) => AddJoin(JoinType.Full, table, on);

    public FromQueryBuilder CrossJoin(Table table)
    {
        if (table is null) throw new ArgumentNullException(nameof(table));
        var next = new List<JoinClause>(_joins) { new JoinClause(JoinType.Cross, table, null) };
        return new FromQueryBuilder(_session, _table, next);
    }

    private FromQueryBuilder AddJoin(JoinType type, Table table, SqlCondition on)
    {
        if (table is null) throw new ArgumentNullException(nameof(table));
        if (on is null) throw new ArgumentNullException(nameof(on));
        var next = new List<JoinClause>(_joins) { new JoinClause(type, table, on) };
        return new FromQueryBuilder(_session, _table, next);
    }

    public SelectQueryBuilder Select(params IColumn[] columns)
    {
        if (columns is null) throw new ArgumentNullException(nameof(columns));
        if (columns.Length == 0)
        {
            throw new InvalidOperationException("Select requires at least one column.");
        }

        var known = new HashSet<Table>(ReferenceEqualityComparer.Instance) { _table };
        foreach (var j in _joins)
        {
            known.Add(j.Table);
        }

        foreach (var column in columns)
        {
            if (!known.Contains(column.Table))
            {
                throw new InvalidOperationException(
                    $"Column '{column}' belongs to table '{column.Table.ClrName}', which is not part of this query. Add the table via a join before selecting from it.");
            }
        }

        return new SelectQueryBuilder(_session, _table, _joins, columns);
    }
}
