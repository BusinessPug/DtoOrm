namespace DtoOrm.Core;

public sealed class SqlRenderContext
{
    private readonly List<SqlParameterValue> _parameters = new();

    public SqlRenderContext(ISqlDialect dialect, bool qualifyColumns = true)
    {
        Dialect = dialect;
        QualifyColumns = qualifyColumns;
    }

    public ISqlDialect Dialect { get; }

    public bool QualifyColumns { get; }

    public IReadOnlyList<SqlParameterValue> Parameters => _parameters;

    public string AddParameter(object? value)
    {
        var name = Dialect.GetParameterName(_parameters.Count);
        _parameters.Add(new SqlParameterValue(name, value));
        return name;
    }

    public string Column(IColumn column)
        => QualifyColumns
            ? column.Render(Dialect)
            : Dialect.QuoteIdentifier(column.DbName);
}
