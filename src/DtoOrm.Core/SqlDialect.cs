namespace DtoOrm.Core;

public interface ISqlDialect
{
    string QuoteIdentifier(string identifier);
    string GetParameterName(int index);
    string LastInsertedIdSelect();
}

public sealed class AnsiLikeDialect : ISqlDialect
{
    public string QuoteIdentifier(string identifier)
        => "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    public string GetParameterName(int index) => "@p" + index;

    public string LastInsertedIdSelect()
        => throw new NotSupportedException("AnsiLikeDialect does not expose a vendor-specific last-inserted-id call.");
}
