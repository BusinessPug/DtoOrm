using DtoOrm.MariaDb;
using Xunit;

namespace DtoOrm.Core.Tests;

public sealed class GroupingQueryTests
{
    [Fact]
    public void GroupBy_with_aggregates_and_having_builds_expected_sql()
    {
        var orders = new OrdersTable();
        var session = new OrmSession(new ThrowingConnectionFactory(), new MariaDbDialect());

        var revenue = Aggregates.Sum(orders.Total, "Revenue");

        var command = session
            .From(orders)
            .Select(orders.CustomerId, Aggregates.Count(orders, "OrderCount"), revenue)
            .Where(orders.IsPaid.Eq(true))
            .GroupBy(orders.CustomerId)
            .Having(revenue.Gt(1000m))
            .OrderByDescending(orders.CustomerId)
            .ToCommand();

        var expectedSql = string.Join(Environment.NewLine, new[]
        {
            "SELECT `o`.`customer_id` AS `CustomerId`, COUNT(*) AS `OrderCount`, SUM(`o`.`total`) AS `Revenue`",
            "FROM `orders` AS `o`",
            "WHERE `o`.`is_paid` = @p0",
            "GROUP BY `o`.`customer_id`",
            "HAVING SUM(`o`.`total`) > @p1",
            "ORDER BY `o`.`customer_id` DESC"
        });

        Assert.Equal(expectedSql, command.Sql);
        Assert.Equal(expectedSql.Length, command.Sql.Length);
        Assert.Equal(6, command.Sql.Split(Environment.NewLine).Length);
        Assert.Equal(2, command.Parameters.Count);
        Assert.True(command.Parameters[0].Value is bool);
        Assert.Equal(1000m, command.Parameters[1].Value);
    }

    [Fact]
    public void GroupBy_multiple_keys_and_count_distinct_builds_expected_sql()
    {
        var orders = new OrdersTable();
        var session = new OrmSession(new ThrowingConnectionFactory(), new MariaDbDialect());

        var command = session
            .From(orders)
            .Select(orders.CustomerId, orders.IsPaid, Aggregates.CountDistinct(orders.Total, "DistinctTotals"))
            .GroupBy(orders.CustomerId, orders.IsPaid)
            .ToCommand();

        var expectedSql = string.Join(Environment.NewLine, new[]
        {
            "SELECT `o`.`customer_id` AS `CustomerId`, `o`.`is_paid` AS `IsPaid`, COUNT(DISTINCT `o`.`total`) AS `DistinctTotals`",
            "FROM `orders` AS `o`",
            "GROUP BY `o`.`customer_id`, `o`.`is_paid`"
        });

        Assert.Equal(expectedSql, command.Sql);
        Assert.Empty(command.Parameters);
    }

    [Fact]
    public void Distinct_emits_select_distinct()
    {
        var orders = new OrdersTable();
        var session = new OrmSession(new ThrowingConnectionFactory(), new MariaDbDialect());

        var command = session
            .From(orders)
            .Select(orders.CustomerId)
            .Distinct()
            .ToCommand();

        var expectedSql = string.Join(Environment.NewLine, new[]
        {
            "SELECT DISTINCT `o`.`customer_id` AS `CustomerId`",
            "FROM `orders` AS `o`"
        });

        Assert.Equal(expectedSql, command.Sql);
    }

    [Fact]
    public void Having_without_group_by_throws()
    {
        var orders = new OrdersTable();
        var session = new OrmSession(new ThrowingConnectionFactory(), new MariaDbDialect());

        var query = session
            .From(orders)
            .Select(orders.CustomerId)
            .Having(Aggregates.Sum(orders.Total).Gt(1m));

        Assert.Throws<InvalidOperationException>(() => query.ToCommand());
    }

    private sealed class OrdersTable : Table
    {
        public OrdersTable() : base("orders", "Orders", "o")
        {
            Id = new Column<int>(this, "id", "Id");
            CustomerId = new Column<int>(this, "customer_id", "CustomerId");
            Total = new Column<decimal>(this, "total", "Total");
            IsPaid = new Column<bool>(this, "is_paid", "IsPaid");
        }

        public Column<int> Id { get; }
        public Column<int> CustomerId { get; }
        public Column<decimal> Total { get; }
        public Column<bool> IsPaid { get; }
    }

    private sealed class ThrowingConnectionFactory : IDbConnectionFactory
    {
        public ValueTask<System.Data.Common.DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
