using System.Collections;

namespace DtoOrm.Core;

public static class ColumnConditions
{
    public static SqlCondition Eq<T>(this Column<T> column, T? value)
        => value is null
            ? new(ctx => $"{ctx.Column(column)} IS NULL")
            : Compare(column, "=", value);

    public static SqlCondition NotEq<T>(this Column<T> column, T? value)
        => value is null
            ? new(ctx => $"{ctx.Column(column)} IS NOT NULL")
            : Compare(column, "<>", value);

    public static SqlCondition Gt<T>(this Column<T> column, T value) => Compare(column, ">", value);
    public static SqlCondition Gte<T>(this Column<T> column, T value) => Compare(column, ">=", value);
    public static SqlCondition Lt<T>(this Column<T> column, T value) => Compare(column, "<", value);
    public static SqlCondition Lte<T>(this Column<T> column, T value) => Compare(column, "<=", value);

    public static SqlCondition EqColumn<T>(this Column<T> left, Column<T> right)
        => CompareColumns(left, "=", right);

    public static SqlCondition NotEqColumn<T>(this Column<T> left, Column<T> right)
        => CompareColumns(left, "<>", right);

    public static SqlCondition GtColumn<T>(this Column<T> left, Column<T> right)
        => CompareColumns(left, ">", right);

    public static SqlCondition GteColumn<T>(this Column<T> left, Column<T> right)
        => CompareColumns(left, ">=", right);

    public static SqlCondition LtColumn<T>(this Column<T> left, Column<T> right)
        => CompareColumns(left, "<", right);

    public static SqlCondition LteColumn<T>(this Column<T> left, Column<T> right)
        => CompareColumns(left, "<=", right);

    public static SqlCondition Like(this Column<string> column, string pattern)
        => Compare(column, "LIKE", pattern);

    public static SqlCondition IsNull<T>(this Column<T> column)
        => new(ctx => $"{ctx.Column(column)} IS NULL");

    public static SqlCondition IsNotNull<T>(this Column<T> column)
        => new(ctx => $"{ctx.Column(column)} IS NOT NULL");

    public static SqlCondition In<T>(this Column<T> column, IEnumerable<T> values)
    {
        if (values is null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        var materialized = values.Cast<object?>().ToArray();
        if (materialized.Length == 0)
        {
            return new SqlCondition(_ => "1 = 0");
        }

        return new SqlCondition(ctx =>
        {
            var names = materialized.Select(ctx.AddParameter);
            return $"{ctx.Column(column)} IN ({string.Join(", ", names)})";
        });
    }

    private static SqlCondition Compare<T>(Column<T> column, string op, object? value)
        => new(ctx =>
        {
            var p = ctx.AddParameter(value);
            return $"{ctx.Column(column)} {op} {p}";
        });

    private static SqlCondition CompareColumns<T>(Column<T> left, string op, Column<T> right)
        => new(ctx => $"{ctx.Column(left)} {op} {ctx.Column(right)}");
}
