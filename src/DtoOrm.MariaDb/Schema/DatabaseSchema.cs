namespace DtoOrm.MariaDb.Schema;

public sealed class DatabaseSchema
{
    public string? Namespace { get; set; }
    public string? Database { get; set; }
    public List<TableSchema> Tables { get; set; } = new();
}

public sealed class TableSchema
{
    public string DbName { get; set; } = "";
    public string ClrName { get; set; } = "";
    public string Alias { get; set; } = "";
    public List<ColumnSchema> Columns { get; set; } = new();
}

public sealed class ColumnSchema
{
    public string DbName { get; set; } = "";
    public string ClrName { get; set; } = "";
    public string ClrType { get; set; } = "object";
    public string DbType { get; set; } = "";
    public bool Nullable { get; set; }
    public bool PrimaryKey { get; set; }
    public int Ordinal { get; set; }
}
