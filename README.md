# DtoOrm

A lightweight, DTO-first ORM for .NET. You write plain C# records or classes; DtoOrm builds parameterized SQL from a typed query DSL and maps result rows back to those types via reflection. There is no runtime schema discovery and no entity tracking.

## Repository layout

```
src/
  DtoOrm.Core        Query DSL, SQL builder, reflection mapper, provider contracts
  DtoOrm.MariaDb     MariaDB/MySQL provider (MySqlConnector)
  DtoOrm.Generator   Roslyn source generator — emits Table/Column constants from a schema snapshot
  DtoOrm.Cli         dotnet global tool — snapshots information_schema to dtoorm.schema.json
samples/
  DtoOrm.Sample      Console project showing every query shape and the SQL it emits
tests/
  DtoOrm.Core.Tests  Unit tests for the SQL builder
```

Detailed documentation for each component lives alongside its source:

- [Core query DSL](src/DtoOrm.Core/README.md)
- [MariaDB provider](src/DtoOrm.MariaDb/README.md)
- [Source generator](src/DtoOrm.Generator/README.md)
- [CLI tool](src/DtoOrm.Cli/README.md)

## Getting started

### 1. Install the CLI and snapshot your schema

```bash
dotnet tool install -g DtoOrm.Cli

dtoorm schema \
  --connection "Server=localhost;Port=3306;Database=mydb;User ID=root;Password=secret;" \
  --output dtoorm.schema.json \
  --namespace MyApp.Data
```

This writes a `dtoorm.schema.json` file that the source generator reads at build time.

### 2. Add the NuGet packages

```bash
dotnet add package DtoOrm.MariaDb
dotnet add package DtoOrm.Generator
```

Include the schema file as an `AdditionalFiles` item so the generator picks it up:

```xml
<ItemGroup>
  <AdditionalFiles Include="dtoorm.schema.json" />
</ItemGroup>
```

### 3. Create a session and query

```csharp
// Minimal setup — no DI required
await using var session = MariaDbOrm.Create(connectionString);

var users = Db.Tables.Users;

var results = await session
	.From(users)
	.Select(users.Id, users.Email)
	.Where(users.IsActive.Eq(true))
	.OrderBy(users.Email)
	.Take(50)
	.ToListAsync<UserDto>();
```

The generated `Db.Tables.Users` object carries a strongly-typed `Column<T>` property for every column in your schema. The DSL methods (`Eq`, `Like`, `Gt`, `In`, etc.) are extension methods on `Column<T>` defined in `DtoOrm.Core`.

### 4. DI registration (ASP.NET Core)

```csharp
builder.Services.AddSingleton<IDbConnectionFactory>(
	new MariaDbConnectionFactory(connectionString));
builder.Services.AddScoped<OrmSession>(sp =>
	new OrmSession(
		sp.GetRequiredService<IDbConnectionFactory>(),
		new MariaDbDialect()));
```

## Requirements

- .NET 8
- MariaDB 10.5+ or MySQL 8+

## License

MIT © Nikolaj Hansen
