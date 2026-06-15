using System.Net;
using System.Net.Http.Json;
using DtoOrm.Api.Features.Departments;
using Xunit;

namespace DtoOrm.Api.Tests.DepartmentEndpointTestsLive;

[Collection(LiveApiTestCollection.Name)]
public sealed class DepartmentEndpointTestsLive
{
    private readonly LiveApiFixture _fixture;

    public DepartmentEndpointTestsLive(LiveApiFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task List_departments_returns_seeded_rows_from_live_api()
    {
        using var client = _fixture.CreateClient();

        var departments = await client.GetFromJsonAsync<List<DepartmentDto>>("/api/departments/");

        Assert.NotNull(departments);
        Assert.Contains(departments, department => department.Code == "CS" && department.Name == "Computer Science");
        Assert.Contains(departments, department => department.Code == "MATH" && department.Name == "Mathematics");
    }

    [Fact]
    public async Task Get_department_returns_not_found_for_missing_live_row()
    {
        using var client = _fixture.CreateClient();

        var response = await client.GetAsync("/api/departments/2147483647");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_department_returns_created_and_can_be_fetched_from_live_api()
    {
        using var client = _fixture.CreateClient();
        var rollback = new DepartmentRollback(_fixture.DatabaseConnectionString);
        var code = NewDepartmentCode();
        rollback.TrackCode(code);

        try
        {
            var response = await client.PostAsJsonAsync(
                "/api/departments/",
                new CreateDepartmentCommand(code, "Live Test Department"));

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            var body = await response.Content.ReadFromJsonAsync<CreatedIdResponse>();
            Assert.NotNull(body);
            rollback.Track(body.Id, code);

            Assert.Equal($"/api/departments/{body.Id}", response.Headers.Location?.OriginalString);

            var created = await client.GetFromJsonAsync<DepartmentDto>($"/api/departments/{body.Id}");
            Assert.NotNull(created);
            Assert.Equal(body.Id, created.Id);
            Assert.Equal(code, created.Code);
            Assert.Equal("Live Test Department", created.Name);
        }
        finally
        {
            await rollback.CleanAsync();
        }
    }

    [Fact]
    public async Task Update_department_uses_route_id_and_persists_live_change()
    {
        using var client = _fixture.CreateClient();
        var rollback = new DepartmentRollback(_fixture.DatabaseConnectionString);
        var originalCode = NewDepartmentCode();
        var updatedCode = NewDepartmentCode();
        rollback.TrackCode(originalCode);
        rollback.TrackCode(updatedCode);

        try
        {
            var createResponse = await client.PostAsJsonAsync(
                "/api/departments/",
                new CreateDepartmentCommand(originalCode, "Live Update Department"));
            createResponse.EnsureSuccessStatusCode();

            var created = await createResponse.Content.ReadFromJsonAsync<CreatedIdResponse>();
            Assert.NotNull(created);
            rollback.Track(created.Id, originalCode);

            var updateResponse = await client.PutAsJsonAsync(
                $"/api/departments/{created.Id}",
                new UpdateDepartmentCommand(999, updatedCode, "Live Updated Department"));

            Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);

            var updated = await client.GetFromJsonAsync<DepartmentDto>($"/api/departments/{created.Id}");
            Assert.NotNull(updated);
            Assert.Equal(created.Id, updated.Id);
            Assert.Equal(updatedCode, updated.Code);
            Assert.Equal("Live Updated Department", updated.Name);
        }
        finally
        {
            await rollback.CleanAsync();
        }
    }

    [Fact]
    public async Task Delete_department_removes_created_live_row()
    {
        using var client = _fixture.CreateClient();
        var rollback = new DepartmentRollback(_fixture.DatabaseConnectionString);
        var code = NewDepartmentCode();
        rollback.TrackCode(code);

        try
        {
            var createResponse = await client.PostAsJsonAsync(
                "/api/departments/",
                new CreateDepartmentCommand(code, "Live Delete Department"));
            createResponse.EnsureSuccessStatusCode();

            var created = await createResponse.Content.ReadFromJsonAsync<CreatedIdResponse>();
            Assert.NotNull(created);
            rollback.Track(created.Id, code);

            var deleteResponse = await client.DeleteAsync($"/api/departments/{created.Id}");
            Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

            var getResponse = await client.GetAsync($"/api/departments/{created.Id}");
            Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
        }
        finally
        {
            await rollback.CleanAsync();
        }
    }

    private static string NewDepartmentCode()
        => $"LT{Guid.NewGuid():N}"[..10].ToUpperInvariant();

    private sealed record CreatedIdResponse(int Id);
}
