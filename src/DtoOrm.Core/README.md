# DtoOrm.Core

The database-agnostic foundation of DtoOrm. It provides the query DSL, the parameterized SQL builder, the reflection-based row mapper, and the provider contracts that database-specific packages implement.

## Contents

- [Concepts](#concepts)
- [OrmSession](#ormsession)
- [Querying](#querying)
- [Conditions](#conditions)
- [Joins](#joins)
- [Insert, Update, Delete](#insert-update-delete)
- [Row mapping](#row-mapping)
- [Provider contracts](#provider-contracts)
- [Extending the library](#extending-the-library)

---

## Concepts

DtoOrm separates schema metadata from domain objects. A `Table` subclass carries the names and types of its columns as `Column<T>` properties. Those objects are only metadata — they are not entities and hold no row data. You pass them to the query DSL to describe what SQL to generate.

The `OrmSession` is the entry point for all queries. It holds a connection factory and a dialect, opens connections on demand, and disposes them after each query.

---

## OrmSession

```csharp
public sealed class OrmSession : IAsyncDisposable
```

**Constructor parameters**

| Parameter | Type | Required | Description |
|---|---|---|---|
| `connectionFactory` | `IDbConnectionFactory` | yes | Provides `DbConnection` instances |
| `dialect` | `ISqlDialect` | yes | Vendor-specific SQL quoting and parameter naming |
| `mapper` | `IRowMapper` | no | Defaults to `ReflectionRowMapper.Instance` |
| `ownedDisposables` | `IEnumerable<IAsyncDisposable>` | no | Disposed when the session is disposed |

`OrmSession` does not open a connection on construction. A new connection is opened per query and closed immediately after.

---

## Querying

All queries begin with `session.From(table)`, which returns a `FromQueryBuilder`. From there you optionally add joins, then call `.Select(columns...)` to get a `SelectQueryBuilder`.

```csharp
var q = session
	.From(Db.Tables.Orders)
	.Select(Db.Tables.Orders.Id, Db.Tables.Orders.Total)
	.Where(Db.Tables.Orders.Total.Gt(100m))
	.OrderBy(Db.Tables.Orders.Total)
	.Skip(20)
	.Take(10);
```

### Execution methods on SelectQueryBuilder

| Method | Returns | Description |
|---|---|---|
| `ToListAsync<TDto>()` | `Task<IReadOnlyList<TDto>>` | Execute and map all rows |
| `FirstOrDefaultAsync<TDto>()` | `Task<TDto?>` | Execute with `LIMIT 1`; returns `null` if no rows |
| `SingleOrDefaultAsync<TDto>()` | `Task<TDto?>` | Execute with `LIMIT 2`; throws if more than one row |
| `ToCommand()` | `QueryCommand` | Build SQL without executing — useful for inspection or logging |

### QueryCommand

`ToCommand()` returns a `QueryCommand` record with two properties:

```csharp
public sealed record QueryCommand(string Sql, IReadOnlyList<SqlParameterValue> Parameters);
```

You can inspect or log the generated SQL before execution.

---

## Conditions

Conditions are built by calling extension methods from `ColumnConditions` on a `Column<T>` value. They return a `SqlCondition`, which can be composed with the `&` (AND), `|` (OR), and `!` (NOT) operators.

```csharp
var condition =
	users.IsActive.Eq(true) &
	(users.LastName.Like("Han%") | users.DepartmentId.IsNull());
```

### Available condition methods

| Method | SQL equivalent |
|---|---|
| `column.Eq(value)` | `column = @p` or `column IS NULL` when value is null |
| `column.NotEq(value)` | `column <> @p` or `column IS NOT NULL` |
| `column.Gt(value)` | `column > @p` |
| `column.Gte(value)` | `column >= @p` |
| `column.Lt(value)` | `column < @p` |
| `column.Lte(value)` | `column <= @p` |
| `column.Like(pattern)` | `column LIKE @p` (string columns only) |
| `column.IsNull()` | `column IS NULL` |
| `column.IsNotNull()` | `column IS NOT NULL` |
| `column.In(values)` | `column IN (@p0, @p1, ...)` |
| `left.EqColumn(right)` | `left = right` (column-to-column comparison) |
| `left.NotEqColumn(right)` | `left <> right` |
| `left.GtColumn(right)` | `left > right` |
| `left.GteColumn(right)` | `left >= right` |
| `left.LtColumn(right)` | `left < right` |
| `left.LteColumn(right)` | `left <= right` |

Column-to-column variants are used primarily for join ON clauses (see below).

`In` with an empty collection emits `1 = 0`, guaranteeing no rows match rather than producing invalid SQL.

### Raw SQL escape hatch

```csharp
var condition = SqlCondition.RawUnsafe("JSON_CONTAINS(tags, '\"vip\"')");
```

Use this only when the DSL cannot express the predicate. The string is inserted verbatim; it is your responsibility to avoid SQL injection.

---

## Joins

Joins are added between `From` and `Select`. Each join method returns a new `FromQueryBuilder`.

```csharp
session
	.From(Db.Tables.People)
	.InnerJoin(Db.Tables.Departments,
		Db.Tables.People.DepartmentId.EqColumn(Db.Tables.Departments.Id))
	.LeftJoin(Db.Tables.Orders,
		Db.Tables.Orders.PersonId.EqColumn(Db.Tables.People.Id))
	.Select(
		Db.Tables.People.FirstName,
		Db.Tables.Departments.Name,
		Db.Tables.Orders.Total)
	.Where(Db.Tables.Orders.Total.Gt(0m));
```

### Supported join types

| Method | SQL keyword |
|---|---|
| `InnerJoin(table, on)` / `Join(table, on)` | `INNER JOIN` |
| `LeftJoin(table, on)` | `LEFT JOIN` |
| `RightJoin(table, on)` | `RIGHT JOIN` |
| `FullJoin(table, on)` | `FULL OUTER JOIN` |
| `CrossJoin(table)` | `CROSS JOIN` (no ON clause) |

`Select` validates that every column you pass belongs to a table that is part of the query. Selecting a column from a table that has not been joined throws `InvalidOperationException` at query-build time, not at runtime.

---

## Insert, Update, Delete

### Insert

```csharp
var id = await session
	.InsertInto(Db.Tables.People)
	.Value(Db.Tables.People.FirstName, "Ada")
	.Value(Db.Tables.People.LastName, "Lovelace")
	.Value(Db.Tables.People.IsActive, true)
	.ExecuteAndReturnIdAsync();
```

| Execution method | Returns | Description |
|---|---|---|
| `ExecuteAsync()` | `Task<int>` | Rows affected |
| `ExecuteAndReturnIdAsync()` | `Task<long>` | Rows affected + last inserted ID in a single round-trip |

### Update

```csharp
await session
	.Update(Db.Tables.People)
	.Set(Db.Tables.People.IsActive, false)
	.Where(Db.Tables.People.Id.Eq(42))
	.ExecuteAsync();
```

Calling `ExecuteAsync` without a `Where` clause throws `InvalidOperationException`. To issue an unconditional update, call `.Unfiltered()` first:

```csharp
await session
	.Update(Db.Tables.People)
	.Set(Db.Tables.People.IsActive, true)
	.Unfiltered()
	.ExecuteAsync();
```

### Delete

```csharp
await session
	.DeleteFrom(Db.Tables.Orders)
	.Where(Db.Tables.Orders.Total.Lt(0m))
	.ExecuteAsync();
```

The same unfiltered guard applies. Call `.Unfiltered()` to allow a delete without a WHERE clause.

---

## Row mapping

The default mapper is `ReflectionRowMapper`. It maps result columns to DTO properties by matching column alias names (the `ClrName` of each `Column<T>`) to property names, case-insensitively.

**For records with a primary constructor**, the mapper calls the constructor, matching parameter names to column aliases. Parameters with no matching column receive their default value.

**For classes with settable properties**, the mapper creates an instance with the parameterless constructor and sets each matching property.

The mapper caches `TypeMap` instances per type in a `ConcurrentDictionary`, so reflection cost is paid only on first use.

### Custom row mapper

Implement `IRowMapper` to replace the default behaviour:

```csharp
public interface IRowMapper
{
	TDto Map<TDto>(DbDataReader reader);
}
```

Pass your implementation to the `OrmSession` constructor:

```csharp
var session = new OrmSession(factory, dialect, mapper: new MyMapper());
```

---

## Provider contracts

### IDbConnectionFactory

```csharp
public interface IDbConnectionFactory
{
	ValueTask<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default);
}
```

Implement this to integrate with connection pooling, multi-tenancy, or any `DbConnection` subclass. The session calls it once per query and disposes the returned connection immediately after.

### ISqlDialect

```csharp
public interface ISqlDialect
{
	string QuoteIdentifier(string identifier);
	string GetParameterName(int index);
	string LastInsertedIdSelect();
}
```

| Method | Purpose |
|---|---|
| `QuoteIdentifier` | Wraps table and column names in vendor-appropriate quotes |
| `GetParameterName` | Produces parameter names like `@p0`, `@p1`, etc. |
| `LastInsertedIdSelect` | Returns the vendor SQL to retrieve the last auto-increment ID |

`AnsiLikeDialect` is provided as a reference implementation using double-quote identifiers. It throws `NotSupportedException` on `LastInsertedIdSelect` because that call is vendor-specific.

---

## Extending the library

### Adding a new database provider

1. Implement `IDbConnectionFactory` wrapping your `DbConnection` type.
2. Implement `ISqlDialect` with the correct quoting character and parameter syntax.
3. Optionally implement `IRowMapper` if you need non-reflection mapping.
4. Pass these to `OrmSession`.

You do not need to subclass any Core type. The session, builders, and condition helpers are all dialect-agnostic.

### Adding a new condition type

`SqlCondition` has an internal constructor that accepts a `Func<SqlRenderContext, string>`. You cannot construct it directly from outside the assembly. To add conditions, add extension methods on `Column<T>` in your own assembly that call the existing factory helpers via `SqlCondition.RawUnsafe` for simple cases, or open a PR to `DtoOrm.Core` adding a new method to `ColumnConditions`.

### Defining a Table manually (without the generator)

The generator is optional. You can hand-write table definitions for small schemas or schemas not hosted in MariaDB:

```csharp
public sealed class UsersTable : Table
{
	internal UsersTable() : base(dbName: "users", clrName: "Users", alias: "u") { }

	public Column<int> Id { get; } = new(/* table assigned after construction via reflection in generator */);
}
```

Because `Column<T>` takes a `Table` reference in its constructor and `Table` is abstract, the cleanest hand-written approach is:

```csharp
public sealed class UsersTable : Table
{
	public UsersTable() : base("users", "Users", "u") { }

	public Column<int>    Id       { get; }
	public Column<string> Email    { get; }
	public Column<bool>   IsActive { get; }

	// initialise columns in the constructor so `this` is available
	public UsersTable() : base("users", "Users", "u")
	{
		Id       = new Column<int>(this,    "id",       "Id");
		Email    = new Column<string>(this, "email",    "Email");
		IsActive = new Column<bool>(this,   "is_active","IsActive");
	}
}

// Singleton instance
public static class Db
{
	public static class Tables
	{
		public static readonly UsersTable Users = new();
	}
}
```
