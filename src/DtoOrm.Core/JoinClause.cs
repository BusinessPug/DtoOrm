namespace DtoOrm.Core;

public enum JoinType
{
    Inner,
    Left,
    Right,
    Full,
    Cross
}

public sealed record JoinClause(JoinType Type, Table Table, SqlCondition? On)
{
    internal string Render(SqlRenderContext ctx)
    {
        var keyword = Type switch
        {
            JoinType.Inner => "INNER JOIN",
            JoinType.Left => "LEFT JOIN",
            JoinType.Right => "RIGHT JOIN",
            JoinType.Full => "FULL OUTER JOIN",
            JoinType.Cross => "CROSS JOIN",
            _ => throw new InvalidOperationException("Unknown join type: " + Type)
        };

        var table = Table.Render(ctx.Dialect);

        if (Type == JoinType.Cross)
        {
            if (On is not null)
            {
                throw new InvalidOperationException("CROSS JOIN does not take an ON clause.");
            }

            return keyword + " " + table;
        }

        if (On is null)
        {
            throw new InvalidOperationException(keyword + " requires an ON clause.");
        }

        return keyword + " " + table + " ON " + On.Render(ctx);
    }
}
