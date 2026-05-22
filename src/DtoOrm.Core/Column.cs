namespace DtoOrm.Core;

public interface IColumn
{
    Table Table { get; }
    string DbName { get; }
    string ClrName { get; }
    Type ClrType { get; }
    string Render(ISqlDialect dialect);
    string RenderSelect(ISqlDialect dialect);
}

public sealed class Column<T> : IColumn
{
    public Column(Table table, string dbName, string clrName)
    {
        Table = table ?? throw new ArgumentNullException(nameof(table));
        DbName = string.IsNullOrWhiteSpace(dbName)
            ? throw new ArgumentException("Column db name cannot be empty.", nameof(dbName))
            : dbName;
        ClrName = string.IsNullOrWhiteSpace(clrName) ? dbName : clrName;
    }

    public Table Table { get; }
    public string DbName { get; }
    public string ClrName { get; }
    public Type ClrType => typeof(T);

    public string Render(ISqlDialect dialect)
        => $"{dialect.QuoteIdentifier(Table.Alias)}.{dialect.QuoteIdentifier(DbName)}";

    public string RenderSelect(ISqlDialect dialect)
        => $"{Render(dialect)} AS {dialect.QuoteIdentifier(ClrName)}";

    public override string ToString() => $"{Table.ClrName}.{ClrName}";
}
