using DtoOrm.Api.Features.Departments;
using Xunit;

namespace DtoOrm.Api.Tests.DepartmentTestsLive;

[Collection(LiveDatabaseTestCollection.Name)]
public sealed class DepartmentTestsLive
{
    private readonly LiveDatabaseFixture _fixture;

    public DepartmentTestsLive(LiveDatabaseFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task List_departments_returns_seeded_rows_from_live_database()
    {
        await using var session = _fixture.CreateSession();

        var departments = await new ListDepartmentsHandler(session).HandleAsync(new ListDepartmentsQuery(), CancellationToken.None);

        Assert.Contains(departments, department => department.Code == "CS" && department.Name == "Computer Science");
        Assert.Contains(departments, department => department.Code == "MATH" && department.Name == "Mathematics");
    }

    [Fact]
    public async Task Get_department_returns_null_for_missing_live_row()
    {
        await using var session = _fixture.CreateSession();

        var department = await new GetDepartmentByIdHandler(session).HandleAsync(
            new GetDepartmentByIdQuery(2147483647),
            CancellationToken.None);

        Assert.Null(department);
    }

    [Fact]
    public async Task Create_department_returns_id_and_can_be_fetched_from_live_database()
    {
        await using var session = _fixture.CreateSession();
        var rollback = new DepartmentRollback(_fixture.DatabaseConnectionString);
        var code = NewDepartmentCode();
        rollback.TrackCode(code);

        try
        {
            var id = await new CreateDepartmentHandler(session).HandleAsync(
                new CreateDepartmentCommand(code, "Live Test Department"),
                CancellationToken.None);
            rollback.Track(id, code);

            var created = await new GetDepartmentByIdHandler(session).HandleAsync(
                new GetDepartmentByIdQuery(id),
                CancellationToken.None);

            Assert.NotNull(created);
            Assert.Equal(id, created.Id);
            Assert.Equal(code, created.Code);
            Assert.Equal("Live Test Department", created.Name);
        }
        finally
        {
            await rollback.CleanAsync();
        }
    }

    [Fact]
    public async Task Update_department_persists_live_database_change()
    {
        await using var session = _fixture.CreateSession();
        var rollback = new DepartmentRollback(_fixture.DatabaseConnectionString);
        var originalCode = NewDepartmentCode();
        var updatedCode = NewDepartmentCode();
        rollback.TrackCode(originalCode);
        rollback.TrackCode(updatedCode);

        try
        {
            var id = await new CreateDepartmentHandler(session).HandleAsync(
                new CreateDepartmentCommand(originalCode, "Live Update Department"),
                CancellationToken.None);
            rollback.Track(id, originalCode);

            var updated = await new UpdateDepartmentHandler(session).HandleAsync(
                new UpdateDepartmentCommand(id, updatedCode, "Live Updated Department"),
                CancellationToken.None);

            Assert.True(updated);

            var department = await new GetDepartmentByIdHandler(session).HandleAsync(
                new GetDepartmentByIdQuery(id),
                CancellationToken.None);

            Assert.NotNull(department);
            Assert.Equal(id, department.Id);
            Assert.Equal(updatedCode, department.Code);
            Assert.Equal("Live Updated Department", department.Name);
        }
        finally
        {
            await rollback.CleanAsync();
        }
    }

    [Fact]
    public async Task Delete_department_removes_created_live_database_row()
    {
        await using var session = _fixture.CreateSession();
        var rollback = new DepartmentRollback(_fixture.DatabaseConnectionString);
        var code = NewDepartmentCode();
        rollback.TrackCode(code);

        try
        {
            var id = await new CreateDepartmentHandler(session).HandleAsync(
                new CreateDepartmentCommand(code, "Live Delete Department"),
                CancellationToken.None);
            rollback.Track(id, code);

            var deleted = await new DeleteDepartmentHandler(session).HandleAsync(
                new DeleteDepartmentCommand(id),
                CancellationToken.None);

            Assert.True(deleted);

            var department = await new GetDepartmentByIdHandler(session).HandleAsync(
                new GetDepartmentByIdQuery(id),
                CancellationToken.None);

            Assert.Null(department);
        }
        finally
        {
            await rollback.CleanAsync();
        }
    }

    private static string NewDepartmentCode()
        => $"LT{Guid.NewGuid():N}"[..10].ToUpperInvariant();
}
