using DtoOrm.MariaDb;
using Xunit;

namespace DtoOrm.Core.Tests;

public sealed class SelectQueryFeatureTests
{
    [Fact]
    public void Join_in_orderby_skip_take_builds_parameterized_sql()
    {
        var people = new PeopleTable();
        var orders = new OrdersTable();
        var session = new OrmSession(new ThrowingConnectionFactory(), new MariaDbDialect());

        var command = session
            .From(people)
            .InnerJoin(orders, people.Id.EqColumn(orders.PersonId))
            .Select(people.FirstName, people.LastName, orders.Total)
            .Where(people.Id.In(new[] { 1, 2, 3 }))
            .OrderByDescending(people.LastName)
            .Skip(10)
            .Take(5)
            .ToCommand();

        Assert.Contains("SELECT `p`.`first_name` AS `FirstName`", command.Sql);
        Assert.Contains("`o`.`total` AS `Total`", command.Sql);
        Assert.Contains("FROM `people` AS `p`", command.Sql);
        Assert.Contains("INNER JOIN `orders` AS `o` ON `p`.`id` = `o`.`person_id`", command.Sql);
        Assert.Contains("WHERE `p`.`id` IN (@p0, @p1, @p2)", command.Sql);
        Assert.Contains("ORDER BY `p`.`last_name` DESC", command.Sql);
        Assert.Contains("LIMIT 5", command.Sql);
        Assert.Contains("OFFSET 10", command.Sql);
        Assert.Equal(3, command.Parameters.Count);
        Assert.DoesNotContain("1, 2, 3", command.Sql);

        // Preventative - the command must be EXACTLY these lines and nothing more .
        var expectedSql = string.Join(Environment.NewLine, new[]
        {
            "SELECT `p`.`first_name` AS `FirstName`, `p`.`last_name` AS `LastName`, `o`.`total` AS `Total`",
            "FROM `people` AS `p`",
            "INNER JOIN `orders` AS `o` ON `p`.`id` = `o`.`person_id`",
            "WHERE `p`.`id` IN (@p0, @p1, @p2)",
            "ORDER BY `p`.`last_name` DESC",
            "LIMIT 5",
            "OFFSET 10"
        });

        Assert.Equal(expectedSql, command.Sql);
        Assert.Equal(expectedSql.Length, command.Sql.Length);
        Assert.Equal(7, command.Sql.Split(Environment.NewLine).Length);
    }

    [Fact]
    public void In_with_empty_values_builds_false_predicate_without_parameters()
    {
        var people = new PeopleTable();
        var session = new OrmSession(new ThrowingConnectionFactory(), new MariaDbDialect());

        var command = session
            .From(people)
            .Select(people.Id)
            .Where(people.Id.In(Array.Empty<int>()))
            .ToCommand();

        Assert.Contains("WHERE 1 = 0", command.Sql);
        Assert.Empty(command.Parameters);

        // Preventative - the command must be EXACTLY these lines and nothing more.
        var expectedSql = string.Join(Environment.NewLine, new[]
        {
            "SELECT `p`.`id` AS `Id`",
            "FROM `people` AS `p`",
            "WHERE 1 = 0"
        });

        Assert.Equal(expectedSql, command.Sql);
        Assert.Equal(expectedSql.Length, command.Sql.Length);
        Assert.Equal(3, command.Sql.Split(Environment.NewLine).Length);
    }

    private sealed class PeopleTable : Table
    {
        public PeopleTable() : base("people", "People", "p")
        {
            Id = new Column<int>(this, "id", "Id");
            FirstName = new Column<string>(this, "first_name", "FirstName");
            LastName = new Column<string>(this, "last_name", "LastName");
        }

        public Column<int> Id { get; }
        public Column<string> FirstName { get; }
        public Column<string> LastName { get; }
    }

    private sealed class OrdersTable : Table
    {
        public OrdersTable() : base("orders", "Orders", "o")
        {
            Id = new Column<int>(this, "id", "Id");
            PersonId = new Column<int>(this, "person_id", "PersonId");
            Total = new Column<decimal>(this, "total", "Total");
        }

        public Column<int> Id { get; }
        public Column<int> PersonId { get; }
        public Column<decimal> Total { get; }
    }

    private sealed class ThrowingConnectionFactory : IDbConnectionFactory
    {
        public ValueTask<System.Data.Common.DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
