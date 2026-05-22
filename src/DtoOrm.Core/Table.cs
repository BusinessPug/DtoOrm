namespace DtoOrm.Core;

/// <summary>
/// Query metadata for a database table. This is not a domain entity.
/// </summary>
public abstract class Table
{
    protected Table(string dbName, string clrName, string alias, string? schema = null)
    {
        DbName = RequireIdentifier(dbName, nameof(dbName));
        ClrName = string.IsNullOrWhiteSpace(clrName) ? dbName : clrName;
        Alias = RequireIdentifier(alias, nameof(alias));
        Schema = string.IsNullOrWhiteSpace(schema) ? null : schema;
    }

    public string DbName { get; }
    public string ClrName { get; }
    public string Alias { get; }
    public string? Schema { get; }

    internal string Render(ISqlDialect dialect)
    {
        var table = Schema is null
            ? dialect.QuoteIdentifier(DbName)
            : $"{dialect.QuoteIdentifier(Schema)}.{dialect.QuoteIdentifier(DbName)}";

        return $"{table} AS {dialect.QuoteIdentifier(Alias)}";
    }

    internal string RenderUnaliased(ISqlDialect dialect)
        => Schema is null
            ? dialect.QuoteIdentifier(DbName)
            : $"{dialect.QuoteIdentifier(Schema)}.{dialect.QuoteIdentifier(DbName)}";

    private static string RequireIdentifier(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Identifier cannot be empty.", parameterName);
        }

        return value;
    }
}
