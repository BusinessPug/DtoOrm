namespace DtoOrm.MariaDb.Schema;

public static class MariaDbTypeMapper
{
    public static string ToClrType(string dataType, bool nullable, string? columnType = null)
    {
        var normalizedDataType = dataType.ToLowerInvariant();
        var normalizedColumnType = columnType?.ToLowerInvariant() ?? "";

        var type = normalizedDataType switch
        {
            "tinyint" when normalizedColumnType.StartsWith("tinyint(1)", StringComparison.OrdinalIgnoreCase) => "bool",
            "bit" when normalizedColumnType.StartsWith("bit(1)", StringComparison.OrdinalIgnoreCase) => "bool",
            "tinyint" => "sbyte",
            "smallint" => "short",
            "mediumint" => "int",
            "int" => "int",
            "integer" => "int",
            "bigint" => "long",
            "bit" => "ulong",
            "float" => "float",
            "double" => "double",
            "decimal" => "decimal",
            "dec" => "decimal",
            "numeric" => "decimal",
            "date" => "DateOnly",
            "datetime" => "DateTime",
            "timestamp" => "DateTime",
            "time" => "TimeSpan",
            "year" => "short",
            "char" => "string",
            "varchar" => "string",
            "tinytext" => "string",
            "text" => "string",
            "mediumtext" => "string",
            "longtext" => "string",
            "json" => "string",
            "enum" => "string",
            "set" => "string",
            "binary" => "byte[]",
            "varbinary" => "byte[]",
            "tinyblob" => "byte[]",
            "blob" => "byte[]",
            "mediumblob" => "byte[]",
            "longblob" => "byte[]",
            _ => "object"
        };

        if (nullable && type is not "string" and not "byte[]" and not "object")
        {
            type += "?";
        }

        return type;
    }
}
