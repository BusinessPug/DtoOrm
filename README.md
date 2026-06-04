# DtoOrm

A lightweight, **DTO-first** ORM for .NET. You write plain C# records or classes; DtoOrm builds **parameterized** SQL from a strongly-typed query DSL and maps result rows back to those types. There is no runtime schema discovery, no change tracking, no lazy loading, and no hidden N+1 surprises — just the SQL you can see, fully typed.

Think of it as the sweet spot between a hand-written `DbCommand` and a heavyweight ORM:

- **Typed, not stringly-typed.** A source generator turns your database schema into `Db.Tables.*` objects with a `Column<T>` for every column, so the compiler and IntelliSense guide every query.
- **Predictable SQL.** Every builder has a `ToCommand()` that returns the exact SQL + parameters, so what you ship is what runs.
- **Safe by default.** Values are always parameterized; `UPDATE`/`DELETE` without a `WHERE` are refused unless you opt in with `Unfiltered()`.

---

## Table of contents

- [How it fits together](#how-it-fits-together)
- [Packages](#packages)
- [Quick start (5 minutes)](#quick-start-5-minutes)
- [The query DSL](#the-query-dsl)
  - [Filtering](#filtering)
  - [Joins](#joins)
  - [Ordering & paging](#ordering--paging)
  - [Grouping & aggregates](#grouping--aggregates)
  - [DISTINCT](#distinct)
  - [Insert / Update / Delete](#insert--update--delete)
- [Dependency injection](#dependency-injection)
- [Component documentation](#component-documentation)
- [Requirements](#requirements)
- [License](#license)

---

## How it fits together

DtoOrm's whole value is the path from a live database connection all the way to autocompleted C#. It happens in four moves:

```
  MariaDB / MySQL                dtoorm.schema.json              Db.Tables.* + Column<T>           Typed queries
  (information_schema)           (committed to the repo)         (generated C#)                   + DTO mapping
		│                               │                               │                               │
		│   dtoorm schema   ───────────▶│   source generator  ─────────▶│   you write, with  ──────────▶│
		│   (CLI snapshots it)          │   (build-time)                │   full IntelliSense           │
```

1. **Snapshot** your schema once with the `dtoorm` CLI → a `dtoorm.schema.json` file you commit.
2. The **source generator** (shipped inside the `DtoOrm` package as a Roslyn analyzer) turns that JSON into `Db.Tables.*` at build time.
3. You **write queries** against those generated objects with full autocomplete.
4. DtoOrm **maps** result rows into your own DTOs.

Because the schema snapshot is a checked-in file, builds are deterministic and offline — no database connection is needed to compile.

---

## Packages

| Package | Install | What it gives you |
|---|---|---|
| **DtoOrm** | `dotnet add package DtoOrm` | The everything-you-need runtime: query DSL, SQL builder, DTO mapper, the MariaDB/MySQL provider, **and** the source generator (bundled as an analyzer). |
| **DtoOrm.Cli** | `dotnet tool install -g DtoOrm.Cli` | The `dtoorm` global tool that snapshots your database schema to `dtoorm.schema.json`. |

> The `DtoOrm` package bundles `DtoOrm.Core` and the generator, so a single `dotnet add package DtoOrm` is all an application project needs. You do **not** add the Core or Generator packages separately.

---

## Quick start (5 minutes)

### 1. Snapshot your schema with the CLI

```bash
dotnet tool install -g DtoOrm.Cli

dtoorm schema \
  --connection "Server=localhost;Port=3306;Database=mydb;User ID=root;Password=secret;" \
  --output dtoorm.schema.json \
  --namespace MyApp.Data
```

This writes `dtoorm.schema.json`. Commit it — it is the contract the generator compiles against.

### 2. Add the package and wire up the generator

```bash
dotnet add package DtoOrm
```

Tell the generator where the snapshot is by adding it as an `AdditionalFiles` item:

```xml
<ItemGroup>
  <AdditionalFiles Include="dtoorm.schema.json" />
</ItemGroup>
```

Rebuild. You now have a generated `MyApp.Data.Db` class with a strongly-typed table object per table.

### 3. Write a query

```csharp
using MyApp.Data; // generated namespace

// Minimal setup — no DI required. Create() owns the connection pool and
// disposes it with the session.
await using var session = MariaDbOrm.Create(connectionString);

var users = Db.Tables.Users;          // generated — full autocomplete on columns

var activeUsers = await session
	.From(users)
	.Select(users.Id, users.Email)
	.Where(users.IsActive.Eq(true))
	.OrderBy(users.Email)
	.Take(50)
	.ToListAsync<UserDto>();          // your own DTO

public sealed record UserDto(int Id, string Email);
```

`Db.Tables.Users` exposes a `Column<T>` for every column; the DSL methods (`Eq`, `Like`, `Gt`, `In`, …) are extension methods on `Column<T>`. Want to see the SQL? Call `.ToCommand()` instead of `.ToListAsync<T>()` and inspect `command.Sql` / `command.Parameters`.

---

## The query DSL

Every example below is pure string-free C#. Call `.ToCommand()` on any builder to get the parameterized SQL.

### Filtering

```csharp
session.From(users)
	.Select(users.Id, users.Email)
	.Where(users.IsActive.Eq(true) & users.Email.Like("%@example.com"))
	.Where(users.Id.In(new[] { 1, 2, 3 }));
```

Supported predicates include `Eq`, `NotEq`, `Gt`, `Gte`, `Lt`, `Lte`, `Like`, `In`, `IsNull`, `IsNotNull`, column-to-column comparisons (`EqColumn`, `GtColumn`, …), and the `&`, `|`, `!` operators. A `null` passed to `Eq`/`NotEq` becomes `IS NULL` / `IS NOT NULL` automatically.

### Joins

```csharp
var orders = Db.Tables.Orders;

session.From(users)
	.InnerJoin(orders, users.Id.EqColumn(orders.UserId))
	.Select(users.Email, orders.Total);
```

`InnerJoin`, `LeftJoin`, `RightJoin`, `FullJoin`, and `CrossJoin` are available. Selecting a column from a table that has not been joined throws a clear exception.

### Ordering & paging

```csharp
session.From(users)
	.Select(users.Id, users.Email)
	.OrderByDescending(users.CreatedAt)
	.Skip(20)
	.Take(10);   // → ORDER BY ... DESC / LIMIT 10 / OFFSET 20
```

### Grouping & aggregates

Build grouped reports with `GroupBy`, the `Aggregates` helpers, and an optional `Having` filter:

```csharp
var orders = Db.Tables.Orders;
var revenue = Aggregates.Sum(orders.Total, "Revenue");

var report = await session
	.From(orders)
	.Select(orders.CustomerId, Aggregates.Count(orders, "OrderCount"), revenue)
	.Where(orders.IsPaid.Eq(true))
	.GroupBy(orders.CustomerId)
	.Having(revenue.Gt(1000m))
	.OrderByDescending(orders.CustomerId)
	.ToListAsync<CustomerRevenueDto>();
```

produces:

```sql
SELECT `o`.`customer_id` AS `CustomerId`, COUNT(*) AS `OrderCount`, SUM(`o`.`total`) AS `Revenue`
FROM `orders` AS `o`
WHERE `o`.`is_paid` = @p0
GROUP BY `o`.`customer_id`
HAVING SUM(`o`.`total`) > @p1
ORDER BY `o`.`customer_id` DESC
```

Available aggregates: `Count`, `Count(column)`, `CountDistinct`, `Sum`, `Avg`, `Min`, `Max`. Each returns an `IColumn`, so you can project it, order by it, or compare it in `Having` (`.Eq`, `.NotEq`, `.Gt`, `.Gte`, `.Lt`, `.Lte`). `Having` without a `GroupBy` is rejected.

### DISTINCT

```csharp
session.From(orders)
	.Select(orders.CustomerId)
	.Distinct();   // → SELECT DISTINCT `o`.`customer_id` ...
```

### Insert / Update / Delete

```csharp
// INSERT, returning the new identity
var newId = await session.InsertInto(users)
	.Value(users.Email, "ada@example.com")
	.Value(users.IsActive, true)
	.ExecuteAndReturnIdAsync();

// UPDATE — a WHERE is required unless you call Unfiltered()
await session.Update(users)
	.Set(users.IsActive, false)
	.Where(users.Id.Eq(newId))
	.ExecuteAsync();

// DELETE
await session.DeleteFrom(users)
	.Where(users.Id.Eq(newId))
	.ExecuteAsync();
```

---

## Dependency injection

For ASP.NET Core, register the connection factory as a singleton (it holds the pool) and the session as scoped (one per request):

```csharp
builder.Services.AddSingleton<IDbConnectionFactory>(
	new MariaDbConnectionFactory(connectionString));

builder.Services.AddScoped<OrmSession>(sp =>
	new OrmSession(
		sp.GetRequiredService<IDbConnectionFactory>(),
		new MariaDbDialect()));
```

Inject `OrmSession` into your repositories or handlers and query as shown above.

---

## Component documentation

Each component has focused docs alongside its source:

- [Core query DSL](src/DtoOrm.Core/README.md) — builders, conditions, aggregates, the mapper, and provider contracts
- [MariaDB provider](src/DtoOrm.MariaDb/README.md) — connection factory, dialect, schema reader, type mapping
- [Source generator](src/DtoOrm.Generator/README.md) — schema JSON format and generated output
- [CLI tool](src/DtoOrm.Cli/README.md) — generating and refreshing the schema snapshot

### Repository layout

```
src/
  DtoOrm.Core        Query DSL, SQL builder, reflection mapper, provider contracts
  DtoOrm.MariaDb     MariaDB/MySQL provider (MySqlConnector) — packed as the "DtoOrm" package
  DtoOrm.Generator   Roslyn source generator — emits Table/Column constants from a schema snapshot
  DtoOrm.Cli         dotnet global tool — snapshots information_schema to dtoorm.schema.json
samples/
  DtoOrm.Sample      Console project showing every query shape and the SQL it emits
  DtoOrm.Api         Minimal ASP.NET Core API showing DI registration
tests/
  DtoOrm.Core.Tests  Unit tests for the SQL builder
```

---

## Requirements

- .NET 10 SDK
- MariaDB 10.5+ or MySQL 8+

---

## License

MIT © Nikolaj Hansen
