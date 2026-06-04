namespace DtoOrm.Core;

/// <summary>
/// A SQL aggregate expression (for example <c>COUNT(*)</c>, <c>SUM(amount)</c>) projected as a
/// selectable column. Because it implements <see cref="IColumn"/>, an aggregate can be used in
/// <c>Select</c>, <c>OrderBy</c>, and <c>Having</c> just like a normal column.
/// </summary>
/// <typeparam name="T">The CLR type the aggregate result is mapped to.</typeparam>
public sealed class AggregateColumn<T> : IColumn
{
    private readonly string _function;
    private readonly IColumn? _inner;
    private readonly bool _distinct;
    private readonly bool _star;

    internal AggregateColumn(string function, IColumn? inner, Table table, string alias, bool distinct = false, bool star = false)
    {
        _function = function ?? throw new ArgumentNullException(nameof(function));
        _inner = inner;
        Table = table ?? throw new ArgumentNullException(nameof(table));
        ClrName = string.IsNullOrWhiteSpace(alias)
            ? throw new ArgumentException("Aggregate alias cannot be empty.", nameof(alias))
            : alias;
        _distinct = distinct;
        _star = star;
    }

    /// <summary>The table the aggregate is anchored to, used for query validation.</summary>
    public Table Table { get; }

    /// <summary>The alias the aggregate is exposed under. Used as the column name in <c>GROUP BY</c>/<c>HAVING</c> scenarios.</summary>
    public string DbName => ClrName;

    /// <summary>The alias the aggregate result is projected as (the DTO property name).</summary>
    public string ClrName { get; }

    /// <summary>The CLR type the aggregate result is mapped to.</summary>
    public Type ClrType => typeof(T);

    /// <summary>Renders the aggregate expression without an alias (for example <c>SUM(`o`.`total`)</c>).</summary>
    public string Render(ISqlDialect dialect)
    {
        var argument = _star
            ? "*"
            : (_distinct ? "DISTINCT " : string.Empty) + _inner!.Render(dialect);

        return $"{_function}({argument})";
    }

    /// <summary>Renders the aggregate expression with its alias (for example <c>SUM(`o`.`total`) AS `Total`</c>).</summary>
    public string RenderSelect(ISqlDialect dialect)
        => $"{Render(dialect)} AS {dialect.QuoteIdentifier(ClrName)}";

    /// <inheritdoc />
    public override string ToString()
        => $"{_function}({(_star ? "*" : _inner?.ToString())})";
}

/// <summary>
/// Factory methods for building SQL aggregate expressions (<c>COUNT</c>, <c>SUM</c>, <c>AVG</c>,
/// <c>MIN</c>, <c>MAX</c>). Pair these with <see cref="SelectQueryBuilder.GroupBy(IColumn[])"/> and
/// <see cref="SelectQueryBuilder.Having(SqlCondition)"/> to build grouped reports.
/// </summary>
/// <example>
/// <code>
/// var report = session
///     .From(orders)
///     .Select(orders.CustomerId, Aggregates.Count(orders), Aggregates.Sum(orders.Total, "Revenue"))
///     .GroupBy(orders.CustomerId)
///     .Having(Aggregates.Sum(orders.Total, "Revenue").Gt(1000m))
///     .ToCommand();
/// </code>
/// </example>
public static class Aggregates
{
    /// <summary>Builds <c>COUNT(*)</c> over the given table, projected as <paramref name="alias"/>.</summary>
    public static AggregateColumn<long> Count(Table table, string alias = "Count")
        => new("COUNT", inner: null, table, alias, star: true);

    /// <summary>Builds <c>COUNT(column)</c>, projected as <paramref name="alias"/>.</summary>
    public static AggregateColumn<long> Count<T>(Column<T> column, string alias = "Count")
        => new("COUNT", column, column.Table, alias);

    /// <summary>Builds <c>COUNT(DISTINCT column)</c>, projected as <paramref name="alias"/>.</summary>
    public static AggregateColumn<long> CountDistinct<T>(Column<T> column, string alias = "Count")
        => new("COUNT", column, column.Table, alias, distinct: true);

    /// <summary>Builds <c>SUM(column)</c>, projected as <paramref name="alias"/>.</summary>
    public static AggregateColumn<decimal> Sum<T>(Column<T> column, string alias = "Sum")
        => new("SUM", column, column.Table, alias);

    /// <summary>Builds <c>AVG(column)</c>, projected as <paramref name="alias"/>.</summary>
    public static AggregateColumn<double> Avg<T>(Column<T> column, string alias = "Avg")
        => new("AVG", column, column.Table, alias);

    /// <summary>Builds <c>MIN(column)</c>, projected as <paramref name="alias"/>.</summary>
    public static AggregateColumn<T> Min<T>(Column<T> column, string alias = "Min")
        => new("MIN", column, column.Table, alias);

    /// <summary>Builds <c>MAX(column)</c>, projected as <paramref name="alias"/>.</summary>
    public static AggregateColumn<T> Max<T>(Column<T> column, string alias = "Max")
        => new("MAX", column, column.Table, alias);
}

/// <summary>
/// Comparison helpers for using aggregate expressions inside a <c>HAVING</c> clause,
/// for example <c>Aggregates.Sum(orders.Total).Gt(1000m)</c>.
/// </summary>
public static class AggregateConditions
{
    /// <summary>Builds <c>aggregate = value</c>.</summary>
    public static SqlCondition Eq<T>(this AggregateColumn<T> aggregate, T value) => Compare(aggregate, "=", value);

    /// <summary>Builds <c>aggregate &lt;&gt; value</c>.</summary>
    public static SqlCondition NotEq<T>(this AggregateColumn<T> aggregate, T value) => Compare(aggregate, "<>", value);

    /// <summary>Builds <c>aggregate &gt; value</c>.</summary>
    public static SqlCondition Gt<T>(this AggregateColumn<T> aggregate, T value) => Compare(aggregate, ">", value);

    /// <summary>Builds <c>aggregate &gt;= value</c>.</summary>
    public static SqlCondition Gte<T>(this AggregateColumn<T> aggregate, T value) => Compare(aggregate, ">=", value);

    /// <summary>Builds <c>aggregate &lt; value</c>.</summary>
    public static SqlCondition Lt<T>(this AggregateColumn<T> aggregate, T value) => Compare(aggregate, "<", value);

    /// <summary>Builds <c>aggregate &lt;= value</c>.</summary>
    public static SqlCondition Lte<T>(this AggregateColumn<T> aggregate, T value) => Compare(aggregate, "<=", value);

    private static SqlCondition Compare<T>(AggregateColumn<T> aggregate, string op, object? value)
        => new(ctx =>
        {
            var parameter = ctx.AddParameter(value);
            return $"{aggregate.Render(ctx.Dialect)} {op} {parameter}";
        });
}
