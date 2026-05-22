# DtoOrm.Cli

A `dotnet` global tool that connects to a MariaDB or MySQL database, reads `information_schema`, and writes a `dtoorm.schema.json` snapshot. That snapshot is consumed at build time by `DtoOrm.Generator` to produce typed table and column constants.

## Contents

- [Installation](#installation)
- [Usage](#usage)
- [Options](#options)
- [Output format](#output-format)
- [Recommended workflow](#recommended-workflow)

---

## Installation

```bash
dotnet tool install -g DtoOrm.Cli
```

To update an existing installation:

```bash
dotnet tool update -g DtoOrm.Cli
```

To install locally within a repository (using a tool manifest):

```bash
dotnet new tool-manifest   # only needed once per repo
dotnet tool install DtoOrm.Cli
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
