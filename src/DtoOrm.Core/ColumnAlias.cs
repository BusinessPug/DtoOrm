namespace DtoOrm.Core;

public sealed class AliasedColumn : IColumn
{
    private readonly IColumn _inner;
    private readonly string _alias;

    public AliasedColumn(IColumn inner, string alias)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _alias = string.IsNullOrWhiteSpace(alias)
            ? throw new ArgumentException("Alias cannot be empty.", nameof(alias))
            : alias;
    }

    public Table Table => _inner.Table;
    public string DbName => _inner.DbName;
    public string ClrName => _alias;
    public Type ClrType => _inner.ClrType;

    public string Render(ISqlDialect dialect) => _inner.Render(dialect);

    public string RenderSelect(ISqlDialect dialect)
        => $"{_inner.Render(dialect)} AS {dialect.QuoteIdentifier(_alias)}";

    public override string ToString() => $"{_inner} AS {_alias}";
}

public static class ColumnAliasExtensions
{
    public static IColumn As(this IColumn column, string alias)
        => new AliasedColumn(column, alias);
}
