using DtoOrm.Core;

namespace DtoOrm.MariaDb;

public sealed class MariaDbDialect : ISqlDialect
{
    public string QuoteIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new ArgumentException("Identifier cannot be empty.", nameof(identifier));
        }

        return "`" + identifier.Replace("`", "``", StringComparison.Ordinal) + "`";
    }

    public string GetParameterName(int index) => "@p" + index;

    public string LastInsertedIdSelect() => "SELECT LAST_INSERT_ID()";
}
