# DtoOrm.MariaDb

MariaDB and MySQL provider for DtoOrm. Implements the Core provider contracts using [MySqlConnector](https://mysqlconnector.net/) and provides a schema reader that introspects `information_schema` to produce the JSON snapshot consumed by the source generator.

> This project is **packed and published as the [`DtoOrm`](https://www.nuget.org/packages/DtoOrm) NuGet package**. It bundles `DtoOrm.Core` and the source generator, so adding `DtoOrm` is all an application needs — you do not install `DtoOrm.Core` or `DtoOrm.Generator` separately.

## Contents

- [Installation](#installation)
- [Creating a session](#creating-a-session)
- [IDbConnectionFactory](#idbconnectionfactory)
- [MariaDbDialect](#mariadbdialect)
- [DI registration](#di-registration)
- [Schema reader](#schema-reader)
- [Extending the provider](#extending-the-provider)

---

## Installation

```bash
dotnet add package DtoOrm
```

Requires .NET 10 and MariaDB 10.5+ or MySQL 8+.

---

## Creating a session

The quickest path is `MariaDbOrm.Create`, which wires the factory and dialect for you and takes ownership of the factory so it is disposed when the session is disposed:

```csharp
await using var session = MariaDbOrm.Create(connectionString);
```

For longer-lived sessions or DI scenarios, construct the pieces separately (see [DI registration](#di-registration)).

---

## IDbConnectionFactory

`MariaDbConnectionFactory` implements `IDbConnectionFactory`. It creates a `MySqlDataSource` from the connection string and opens one connection per call to `OpenConnectionAsync`. The data source is created once and reused, so connection pooling works as normal.

```csharp
var factory = new MariaDbConnectionFactory(connectionString);
```

`MariaDbConnectionFactory` implements `IAsyncDisposable`. Dispose it when the application shuts down to drain the connection pool cleanly. When you use `MariaDbOrm.Create`, the factory is registered as an owned disposable and disposed with the session.

---

## MariaDbDialect

`MariaDbDialect` implements `ISqlDialect`:

- Identifiers are wrapped in backtick quotes. Backticks in names are escaped by doubling.
- Parameters are named `@p0`, `@p1`, etc.
- `LastInsertedIdSelect` returns `SELECT LAST_INSERT_ID()`, appended to INSERT statements when you call `ExecuteAndReturnIdAsync`.

You do not normally need to interact with this class directly.

---

## DI registration

Register the factory as a singleton (it holds the pool) and the session as scoped (one per request):

```csharp
builder.Services.AddSingleton<IDbConnectionFactory>(
    new MariaDbConnectionFactory(connectionString));

builder.Services.AddScoped<OrmSession>(sp =>
    new OrmSession(
        sp.GetRequiredService<IDbConnectionFactory>(),
        new MariaDbDialect()));
```

Inject `OrmSession` directly into your repositories or handlers. Avoid injecting `IDbConnectionFactory` outside of the composition root unless you need to manage session lifetimes manually.

---

## Schema reader

`MariaDbSchemaReader` queries `information_schema.columns` and writes the result as a `dtoorm.schema.json` file. The CLI tool (`DtoOrm.Cli`) calls this internally. You can also call it from code if you want to automate schema refreshes in a build script:

```csharp
var reader = new MariaDbSchemaReader(connectionString);
var schema = await reader.ReadAsync(database: "mydb", generatedNamespace: "MyApp.Data");
await MariaDbSchemaReader.WriteJsonAsync(schema, "dtoorm.schema.json");
```

The `database` parameter is optional. When omitted, the schema of the database in the connection string is used.

The output format is described in the [Generator README](../DtoOrm.Generator/README.md).

### Type mapping

MariaDB column types are mapped to CLR types as follows:

| MariaDB type | CLR type |
|---|---|
| `tinyint(1)` | `bool` |
| `tinyint`, `smallint`, `mediumint`, `int` | `int` |
| `bigint` | `long` |
| `float` | `float` |
| `double` | `double` |
| `decimal`, `numeric` | `decimal` |
| `char`, `varchar`, `text`, `mediumtext`, `longtext`, `enum`, `set` | `string` |
| `date`, `datetime`, `timestamp` | `System.DateTime` |
| `time` | `System.TimeSpan` |
| `binary`, `varbinary`, `blob`, `mediumblob`, `longblob` | `byte[]` |
| `json` | `string` |
| `bit(1)` | `bool` |
| `bit(n > 1)` | `ulong` |
| anything else | `object` |

Nullable columns get the nullable CLR counterpart (`int?`, `bool?`, etc.). Primary key columns and non-nullable columns get the non-nullable type.

---

## Extending the provider

### Adjusting type mapping

`MariaDbTypeMapper` is a standalone static class with a single method `Map(string dataType, string columnType, bool isNullable)`. You cannot currently override it without forking the package. If you need different mappings, consider writing the `dtoorm.schema.json` by hand or with a custom script and feeding it directly to the generator.

### Supporting a different MySQL-compatible server

Because `MariaDbDialect` only controls quoting and parameter naming, it works unchanged against any server that:

- Uses backtick-quoted identifiers
- Uses `@param` style parameters
- Exposes `LAST_INSERT_ID()`

Servers that meet these criteria (e.g. PlanetScale, TiDB in MySQL mode) should work with the existing dialect. If your server differs, implement `ISqlDialect` directly — see the [Core README](../DtoOrm.Core/README.md#provider-contracts).
