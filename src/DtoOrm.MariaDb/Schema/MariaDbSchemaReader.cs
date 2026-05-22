using System.Text.Json;
using MySqlConnector;

namespace DtoOrm.MariaDb.Schema;

public sealed class MariaDbSchemaReader
{
    private readonly string _connectionString;

    public MariaDbSchemaReader(string connectionString)
    {
        _connectionString = string.IsNullOrWhiteSpace(connectionString)
            ? throw new ArgumentException("Connection string cannot be empty.", nameof(connectionString))
            : connectionString;
    }

    public async Task<DatabaseSchema> ReadAsync(
        string? database = null,
        string? generatedNamespace = null,
        CancellationToken cancellationToken = default)
    {
        await using var dataSource = new MySqlDataSourceBuilder(_connectionString).Build();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT
                table_schema,
                table_name,
                column_name,
                ordinal_position,
                data_type,
                column_type,
                is_nullable,
                column_key
            FROM information_schema.columns
            WHERE table_schema = COALESCE(@schema, DATABASE())
            ORDER BY table_name, ordinal_position;
            """;

        var schemaParameter = cmd.CreateParameter();
        schemaParameter.ParameterName = "@schema";
        schemaParameter.Value = string.IsNullOrWhiteSpace(database) ? DBNull.Value : database;
        cmd.Parameters.Add(schemaParameter);

        var byTable = new Dictionary<string, TableSchema>(StringComparer.OrdinalIgnoreCase);
        string? actualDatabase = database;

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            actualDatabase ??= reader.GetString(reader.GetOrdinal("table_schema"));

            var tableName = reader.GetString(reader.GetOrdinal("table_name"));
            if (!byTable.TryGetValue(tableName, out var table))
            {
                table = new TableSchema
                {
                    DbName = tableName,
                    ClrName = SchemaName.ToPascalCaseIdentifier(tableName),
                    Alias = MakeUniqueAlias(tableName, byTable.Values.Select(t => t.Alias).ToHashSet(StringComparer.OrdinalIgnoreCase))
                };

                byTable.Add(tableName, table);
            }

            var dataType = reader.GetString(reader.GetOrdinal("data_type"));
            var columnType = reader.GetString(reader.GetOrdinal("column_type"));
            var isNullable = string.Equals(reader.GetString(reader.GetOrdinal("is_nullable")), "YES", StringComparison.OrdinalIgnoreCase);
            var columnName = reader.GetString(reader.GetOrdinal("column_name"));

            table.Columns.Add(new ColumnSchema
            {
                DbName = columnName,
                ClrName = SchemaName.ToPascalCaseIdentifier(columnName),
                DbType = columnType,
                ClrType = MariaDbTypeMapper.ToClrType(dataType, isNullable, columnType),
                Nullable = isNullable,
                PrimaryKey = string.Equals(reader.GetString(reader.GetOrdinal("column_key")), "PRI", StringComparison.OrdinalIgnoreCase),
                Ordinal = Convert.ToInt32(reader.GetValue(reader.GetOrdinal("ordinal_position")))
            });
        }

        return new DatabaseSchema
        {
            Namespace = generatedNamespace,
            Database = actualDatabase,
            Tables = byTable.Values.OrderBy(t => t.DbName, StringComparer.OrdinalIgnoreCase).ToList()
        };
    }

    public static async Task WriteJsonAsync(
        DatabaseSchema schema,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(schema, options), cancellationToken)
            .ConfigureAwait(false);
    }

    private static string MakeUniqueAlias(string tableName, HashSet<string> usedAliases)
    {
        var baseAlias = SchemaName.ToAlias(tableName);
        var alias = baseAlias;
        var index = 2;

        while (usedAliases.Contains(alias))
        {
            alias = baseAlias + index;
            index++;
        }

        return alias;
    }
}
