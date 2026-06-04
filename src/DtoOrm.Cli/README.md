# DtoOrm.Cli

A `dotnet` global tool that connects to a MariaDB or MySQL database, reads `information_schema`, and writes a `dtoorm.schema.json` snapshot. That snapshot is consumed at build time by the source generator bundled in the [`DtoOrm`](https://www.nuget.org/packages/DtoOrm) package to produce typed table and column constants.

## Where this fits

This CLI is step 1 of the DtoOrm workflow. It is the **only** part that needs a live database connection:

```
dtoorm schema   ──▶   dtoorm.schema.json   ──▶   source generator   ──▶   Db.Tables.* (typed)   ──▶   your queries
(this tool)           (commit to repo)          (in the DtoOrm pkg)       (generated at build)        (full IntelliSense)
```

You run the CLI once (and again after each migration); everything downstream is offline and deterministic from the committed JSON file. The generator ships **inside the `DtoOrm` NuGet package** as a Roslyn analyzer — there is no separate generator package to install. See the [root README](../../README.md) for the full picture.

## Contents

- [Installation](#installation)
- [Usage](#usage)
- [Options](#options)
- [Output format](#output-format)
- [Recommended workflow](#recommended-workflow)

---

## Installation

```bash
dotnet tool install -g DtoOrm.Cli --prerelease
```

Requires the .NET 10 SDK.

> The package is currently published as a prerelease (for example `0.3.0-beta`). `dotnet tool install` skips prerelease versions by default, so you must pass `--prerelease` (or an explicit `--version 0.3.0-beta`); otherwise the install fails because no matching stable version is found.

To update an existing installation:

```bash
dotnet tool update -g DtoOrm.Cli --prerelease
```

To install locally within a repository (using a tool manifest):

```bash
dotnet new tool-manifest   # only needed once per repo
dotnet tool install DtoOrm.Cli --prerelease
```

When installed locally, run the tool with `dotnet dtoorm` instead of `dtoorm`.

---

## Usage

```
dtoorm schema --connection "<connection-string>" --output <path> [--database <name>] [--namespace <namespace>]
```

**Example:**

```bash
dtoorm schema \
  --connection "Server=localhost;Port=3306;Database=mydb;User ID=root;Password=secret;" \
  --output dtoorm.schema.json \
  --namespace MyApp.Data
```

On success, the tool prints the path of the written file along with the table and column counts:

```
Wrote DtoOrm schema snapshot: C:\projects\myapp\dtoorm.schema.json
Tables: 12
Columns: 87
```

On failure, a description of the error is written to stderr and the process exits with code 2.

---

## Options

| Option | Required | Description |
|---|---|---|
| `--connection <string>` | yes | MySqlConnector connection string. |
| `--output <path>` | yes | File path to write. The file is created or overwritten. |
| `--database <name>` | no | Database (schema) name to introspect. Defaults to the database named in the connection string. |
| `--namespace <name>` | no | C# namespace written into the JSON and used by the generator. Defaults to `DtoOrm.Generated`. |
| `--help` / `-h` | — | Print usage and exit. |

### Connection string format

The connection string is passed directly to MySqlConnector. A minimal example:

```
Server=localhost;Port=3306;Database=mydb;User ID=appuser;Password=secret;
```

For production environments, prefer reading the connection string from an environment variable rather than passing it on the command line:

```bash
dtoorm schema \
  --connection "$DTOORM_CONNECTION" \
  --output dtoorm.schema.json \
  --namespace MyApp.Data
```

---

## Output format

The tool writes a JSON file in the format expected by `DtoOrm.Generator`. See the [Generator README](../DtoOrm.Generator/README.md#schema-file-format) for the full field reference.

A minimal example for a two-table database:

```json
{
  "namespace": "MyApp.Data",
  "database": "mydb",
  "tables": [
    {
      "dbName": "users",
      "clrName": "Users",
      "alias": "use",
      "columns": [
        { "dbName": "id",       "clrName": "Id",      "clrType": "int",    "ordinal": 1 },
        { "dbName": "email",    "clrName": "Email",   "clrType": "string", "ordinal": 2 },
        { "dbName": "is_active","clrName": "IsActive","clrType": "bool",   "ordinal": 3 }
      ]
    }
  ]
}
```

The alias is derived automatically from the first three alphanumeric characters of the table name when no alias is set in the schema. You can edit the file after generation to set explicit aliases, add or remove tables, or override CLR types — the CLI will overwrite your edits the next time it runs, so treat the generated file as a starting point and use version control to preserve intentional changes.

---

## Recommended workflow

1. Run the CLI once after your initial database setup to generate the baseline `dtoorm.schema.json`.
2. Commit the file to source control alongside your application code.
3. After each database migration, re-run the CLI and commit the updated schema file as part of the same commit or PR as the migration.
4. In CI, run the CLI before building if you want to assert that the committed schema file matches the current database state.

The generator runs entirely at build time from the committed file, so developers who do not have a local database can still build and compile without running the CLI.

### From snapshot to typed query

Once the snapshot is committed and the `DtoOrm` package is referenced (with the schema added as an `AdditionalFiles` item), the generated `Db.Tables.*` objects are immediately available with full autocomplete:

```csharp
using MyApp.Data; // the --namespace you passed to the CLI

await using var session = MariaDbOrm.Create(connectionString);

var users = Db.Tables.Users; // generated from dtoorm.schema.json

var emails = await session
    .From(users)
    .Select(users.Email)
    .Where(users.IsActive.Eq(true))
    .ToListAsync<string>();
```

See the [root README](../../README.md#the-query-dsl) for the full query DSL, including joins, grouping, and aggregates.
