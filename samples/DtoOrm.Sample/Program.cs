using DtoOrm.Core;
using DtoOrm.MariaDb;
using DtoOrm.Sample.Generated;

var people = Db.Tables.People;
var departments = Db.Tables.Departments;
var orders = Db.Tables.Orders;

var session = new OrmSession(
    new ThrowingConnectionFactory(),
    new MariaDbDialect());

PrintHeader("DtoOrm — code to SQL showcase");

Show(
    "Simple SELECT with WHERE and LIMIT",
    """
    session.From(people)
        .Select(people.FirstName, people.LastName)
        .Where(people.IsActive.Eq(true) & people.LastName.Like("Han%"))
        .Take(100)
        .ToCommand();
    """,
    session.From(people)
        .Select(people.FirstName, people.LastName)
        .Where(people.IsActive.Eq(true) & people.LastName.Like("Han%"))
        .Take(100)
        .ToCommand());

Show(
    "INNER JOIN — people with their department",
    """
    session.From(people)
        .InnerJoin(departments, people.DepartmentId.EqColumn(departments.Id))
        .Select(people.FirstName, people.LastName, departments.Name)
        .Where(people.IsActive.Eq(true))
        .ToCommand();
    """,
    session.From(people)
        .InnerJoin(departments, people.DepartmentId.EqColumn(departments.Id))
        .Select(people.FirstName, people.LastName, departments.Name)
        .Where(people.IsActive.Eq(true))
        .ToCommand());

Show(
    "LEFT JOIN — every person, department if any",
    """
    session.From(people)
        .LeftJoin(departments, people.DepartmentId.EqColumn(departments.Id))
        .Select(people.Id, people.FirstName, departments.Name)
        .ToCommand();
    """,
    session.From(people)
        .LeftJoin(departments, people.DepartmentId.EqColumn(departments.Id))
        .Select(people.Id, people.FirstName, departments.Name)
        .ToCommand());

Show(
    "RIGHT JOIN — every department, people if any",
    """
    session.From(people)
        .RightJoin(departments, people.DepartmentId.EqColumn(departments.Id))
        .Select(departments.Name, people.FirstName)
        .ToCommand();
    """,
    session.From(people)
        .RightJoin(departments, people.DepartmentId.EqColumn(departments.Id))
        .Select(departments.Name, people.FirstName)
        .ToCommand());

Show(
    "FULL OUTER JOIN — union of both sides",
    """
    session.From(people)
        .FullJoin(departments, people.DepartmentId.EqColumn(departments.Id))
        .Select(people.FirstName, departments.Name)
        .ToCommand();
    """,
    session.From(people)
        .FullJoin(departments, people.DepartmentId.EqColumn(departments.Id))
        .Select(people.FirstName, departments.Name)
        .ToCommand());

Show(
    "CROSS JOIN — Cartesian product",
    """
    session.From(people)
        .CrossJoin(departments)
        .Select(people.FirstName, departments.Name)
        .Take(10)
        .ToCommand();
    """,
    session.From(people)
        .CrossJoin(departments)
        .Select(people.FirstName, departments.Name)
        .Take(10)
        .ToCommand());

Show(
    "Multi-join — people, department, and orders",
    """
    session.From(people)
        .InnerJoin(departments, people.DepartmentId.EqColumn(departments.Id))
        .LeftJoin(orders, orders.PersonId.EqColumn(people.Id))
        .Select(people.FirstName, departments.Name, orders.Total)
        .Where(orders.Total.Gt(100m))
        .ToCommand();
    """,
    session.From(people)
        .InnerJoin(departments, people.DepartmentId.EqColumn(departments.Id))
        .LeftJoin(orders, orders.PersonId.EqColumn(people.Id))
        .Select(people.FirstName, departments.Name, orders.Total)
        .Where(orders.Total.Gt(100m))
        .ToCommand());

Show(
    "INSERT",
    """
    session.InsertInto(people)
        .Value(people.FirstName, "Ada")
        .Value(people.LastName, "Lovelace")
        .Value(people.IsActive, true)
        .Value(people.DepartmentId, 1)
        .ToCommand();
    """,
    session.InsertInto(people)
        .Value(people.FirstName, "Ada")
        .Value(people.LastName, "Lovelace")
        .Value(people.IsActive, true)
        .Value(people.DepartmentId, 1)
        .ToCommand());

Show(
    "UPDATE with WHERE",
    """
    session.Update(people)
        .Set(people.IsActive, false)
        .Set(people.DepartmentId, 2)
        .Where(people.Id.Eq(42))
        .ToCommand();
    """,
    session.Update(people)
        .Set(people.IsActive, false)
        .Set(people.DepartmentId, 2)
        .Where(people.Id.Eq(42))
        .ToCommand());

Show(
    "DELETE with WHERE",
    """
    session.DeleteFrom(orders)
        .Where(orders.Total.Lt(1m))
        .ToCommand();
    """,
    session.DeleteFrom(orders)
        .Where(orders.Total.Lt(1m))
        .ToCommand());

Console.WriteLine();
Console.WriteLine("Done. The samples above never opened a connection — they only rendered SQL.");

static void Show(string title, string code, QueryCommand command)
{
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("-- " + title + " " + new string('-', Math.Max(0, 70 - title.Length)));
    Console.ResetColor();

    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine("C# code:");
    Console.ResetColor();
    Console.WriteLine(Indent(code));

    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine("SQL emitted:");
    Console.ResetColor();
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine(Indent(command.Sql));
    Console.ResetColor();

    if (command.Parameters.Count > 0)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("Parameters:");
        Console.ResetColor();
        foreach (var p in command.Parameters)
        {
            Console.WriteLine($"    {p.Name} = {p.Value ?? "NULL"}");
        }
    }
}

static string Indent(string text)
{
    return string.Join(Environment.NewLine, text
        .Split('\n')
        .Select(line => "    " + line.TrimEnd('\r')));
}

static void PrintHeader(string text)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine(new string('=', 72));
    Console.WriteLine("  " + text);
    Console.WriteLine(new string('=', 72));
    Console.ResetColor();
}

sealed class ThrowingConnectionFactory : IDbConnectionFactory
{
    public ValueTask<System.Data.Common.DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This sample only renders SQL.");
}
