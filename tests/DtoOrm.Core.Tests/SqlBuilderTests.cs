using DtoOrm.Core;
using DtoOrm.MariaDb;
using Xunit;

namespace DtoOrm.Core.Tests;

public sealed class SqlBuilderTests
{
    [Fact]
    public void Select_where_take_builds_parameterized_sql()
    {
        var t = new PeopleTable();
        var session = new OrmSession(new ThrowingConnectionFactory(), new MariaDbDialect());

        var command = session
            .From(t)
            .Select(t.FirstName, t.LastName)
            .Where(t.IsActive.Eq(true) & t.LastName.Like("Han%"))
            .Take(10)
            .ToCommand();

        Assert.Contains("SELECT `p`.`first_name` AS `FirstName`", command.Sql);
        Assert.Contains("FROM `people` AS `p`", command.Sql);
        Assert.Contains("WHERE", command.Sql);
        Assert.Contains("@p0", command.Sql);
        Assert.Contains("@p1", command.Sql);
        Assert.DoesNotContain("Han%", command.Sql);
        Assert.Equal(2, command.Parameters.Count);
    }

    private sealed class PeopleTable : Table
    {
        public PeopleTable() : base("people", "People", "p")
        {
            Id = new Column<int>(this, "id", "Id");
            FirstName = new Column<string>(this, "first_name", "FirstName");
            LastName = new Column<string>(this, "last_name", "LastName");
            IsActive = new Column<bool>(this, "is_active", "IsActive");
        }

        public Column<int> Id { get; }
        public Column<string> FirstName { get; }
        public Column<string> LastName { get; }
        public Column<bool> IsActive { get; }
    }

    private sealed class ThrowingConnectionFactory : IDbConnectionFactory
    {
        public ValueTask<System.Data.Common.DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
