using DtoOrm.MariaDb.Schema;

static int Fail(string message)
{
    Console.Error.WriteLine(message);
    Console.Error.WriteLine();
    PrintHelp();
    return 1;
}

static void PrintHelp()
{
    Console.WriteLine("""
        DtoOrm CLI

        Usage:
          dtoorm schema --connection "<connection-string>" --output dtoorm.schema.json [--database mydb] [--namespace MyApp.Data]

        Example:
          dtoorm schema \
            --connection "Server=localhost;Port=3306;Database=mydb;User ID=root;Password=secret;" \
            --output dtoorm.schema.json \
            --namespace MyApp.Data
        """);
}

if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
{
    PrintHelp();
    return 0;
}

if (!string.Equals(args[0], "schema", StringComparison.OrdinalIgnoreCase))
{
    return Fail("Unknown command. Expected 'schema'.");
}

var options = ParseOptions(args.Skip(1).ToArray());

if (!options.TryGetValue("connection", out var connectionString) || string.IsNullOrWhiteSpace(connectionString))
{
    return Fail("Missing required option: --connection");
}

if (!options.TryGetValue("output", out var output) || string.IsNullOrWhiteSpace(output))
{
    return Fail("Missing required option: --output");
}

options.TryGetValue("database", out var database);
options.TryGetValue("namespace", out var ns);

try
{
    var reader = new MariaDbSchemaReader(connectionString);
    var schema = await reader.ReadAsync(database, ns);
    await MariaDbSchemaReader.WriteJsonAsync(schema, output);

    Console.WriteLine($"Wrote DtoOrm schema snapshot: {Path.GetFullPath(output)}");
    Console.WriteLine($"Tables: {schema.Tables.Count}");
    Console.WriteLine($"Columns: {schema.Tables.Sum(t => t.Columns.Count)}");

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine("Schema generation failed:");
    Console.Error.WriteLine(ex.Message);
    return 2;
}

static Dictionary<string, string> ParseOptions(string[] args)
{
    var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    for (var i = 0; i < args.Length; i++)
    {
        var token = args[i];

        if (!token.StartsWith("--", StringComparison.Ordinal))
        {
            continue;
        }

        var key = token[2..];

        if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
        {
            result[key] = "true";
            continue;
        }

        result[key] = args[++i];
    }

    return result;
}
