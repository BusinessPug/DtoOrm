namespace DtoOrm.Core;

public sealed class SqlCondition
{
    private readonly Func<SqlRenderContext, string> _render;

    internal SqlCondition(Func<SqlRenderContext, string> render)
    {
        _render = render ?? throw new ArgumentNullException(nameof(render));
    }

    internal string Render(SqlRenderContext context) => _render(context);

    public static SqlCondition operator &(SqlCondition left, SqlCondition right)
        => new(ctx => $"({left.Render(ctx)} AND {right.Render(ctx)})");

    public static SqlCondition operator |(SqlCondition left, SqlCondition right)
        => new(ctx => $"({left.Render(ctx)} OR {right.Render(ctx)})");

    public static SqlCondition operator !(SqlCondition condition)
        => new(ctx => $"(NOT {condition.Render(ctx)})");

    public static SqlCondition RawUnsafe(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            throw new ArgumentException("Raw SQL condition cannot be empty.", nameof(sql));
        }

        return new SqlCondition(_ => sql);
    }
}
