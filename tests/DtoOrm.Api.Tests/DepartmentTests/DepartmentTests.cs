using DtoOrm.Api.Features.Departments;
using Xunit;

namespace DtoOrm.Api.Tests.DepartmentTests;

public sealed class DepartmentTests
{
    private readonly RecordingDatabase _database = new();

    [Fact]
    public async Task List_departments_returns_rows_from_database_session()
    {
        _database.EnqueueReaderRows(
            Row(("Id", 1), ("Code", "CS"), ("Name", "Computer Science")),
            Row(("Id", 2), ("Code", "MATH"), ("Name", "Mathematics")));

        await using var session = _database.CreateSession();
        var departments = await new ListDepartmentsHandler(session).HandleAsync(new ListDepartmentsQuery(), CancellationToken.None);

        Assert.Collection(
            departments,
            department =>
            {
                Assert.Equal(1, department.Id);
                Assert.Equal("CS", department.Code);
                Assert.Equal("Computer Science", department.Name);
            },
            department =>
            {
                Assert.Equal(2, department.Id);
                Assert.Equal("MATH", department.Code);
                Assert.Equal("Mathematics", department.Name);
            });

        var command = Assert.Single(_database.Commands);
        Assert.Equal(SelectDepartmentsSql(), command.Sql);
        Assert.Empty(command.Parameters);
    }

    [Fact]
    public async Task Get_department_returns_null_when_database_returns_no_rows()
    {
        _database.EnqueueReaderRows();

        await using var session = _database.CreateSession();
        var department = await new GetDepartmentByIdHandler(session).HandleAsync(new GetDepartmentByIdQuery(999), CancellationToken.None);

        Assert.Null(department);

        var command = Assert.Single(_database.Commands);
        Assert.Equal(
            SelectDepartmentsSql("WHERE `d`.`id` = @p0", "LIMIT 2"),
            command.Sql);
        AssertParameter(command, "@p0", 999);
    }

    [Fact]
    public async Task Create_department_inserts_and_returns_database_id()
    {
        _database.EnqueueScalar(17L);

        await using var session = _database.CreateSession();
        var id = await new CreateDepartmentHandler(session).HandleAsync(
            new CreateDepartmentCommand("ENG", "Engineering"),
            CancellationToken.None);

        Assert.Equal(17, id);

        var command = Assert.Single(_database.Commands);
        Assert.Equal(
            Lines(
                "INSERT INTO `departments` (`code`, `name`)",
                "VALUES (@p0, @p1);",
                "SELECT LAST_INSERT_ID()"),
            command.Sql);
        AssertParameter(command, "@p0", "ENG");
        AssertParameter(command, "@p1", "Engineering");
    }

    [Fact]
    public async Task Update_department_uses_command_id_and_returns_true_when_row_is_affected()
    {
        _database.EnqueueNonQuery(1);

        await using var session = _database.CreateSession();
        var updated = await new UpdateDepartmentHandler(session).HandleAsync(
            new UpdateDepartmentCommand(5, "BIO", "Biology"),
            CancellationToken.None);

        Assert.True(updated);

        var command = Assert.Single(_database.Commands);
        Assert.Equal(
            Lines(
                "UPDATE `departments`",
                "SET `code` = @p0, `name` = @p1",
                "WHERE `id` = @p2"),
            command.Sql);
        AssertParameter(command, "@p0", "BIO");
        AssertParameter(command, "@p1", "Biology");
        AssertParameter(command, "@p2", 5);
    }

    [Fact]
    public async Task Delete_department_returns_false_when_no_row_is_affected()
    {
        _database.EnqueueNonQuery(0);

        await using var session = _database.CreateSession();
        var deleted = await new DeleteDepartmentHandler(session).HandleAsync(
            new DeleteDepartmentCommand(404),
            CancellationToken.None);

        Assert.False(deleted);

        var command = Assert.Single(_database.Commands);
        Assert.Equal(
            Lines(
                "DELETE FROM `departments`",
                "WHERE `id` = @p0"),
            command.Sql);
        AssertParameter(command, "@p0", 404);
    }

    private static Dictionary<string, object?> Row(params (string Name, object? Value)[] fields)
        => fields.ToDictionary(field => field.Name, field => field.Value);

    private static string SelectDepartmentsSql(params string[] trailingLines)
    {
        var lines = new[]
        {
            "SELECT `d`.`id` AS `Id`, `d`.`code` AS `Code`, `d`.`name` AS `Name`",
            "FROM `departments` AS `d`"
        };

        return Lines(lines.Concat(trailingLines).ToArray());
    }

    private static string Lines(params string[] lines)
        => string.Join(Environment.NewLine, lines);

    private static void AssertParameter(ExecutedDbCommand command, string name, object? value)
    {
        var parameter = Assert.Single(command.Parameters, parameter => parameter.Name == name);
        Assert.Equal(value, parameter.Value);
    }
}
