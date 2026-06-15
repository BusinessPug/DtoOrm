using System.Net;
using System.Net.Http.Json;
using DtoOrm.Api.Features.Departments;
using Xunit;

namespace DtoOrm.Api.Tests;

public sealed class DepartmentEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;
    private readonly HttpClient _client;

    public DepartmentEndpointTests(ApiTestFactory factory)
    {
        _factory = factory;
        _factory.Departments.Reset();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task List_departments_returns_ok_with_json_body()
    {
        var departments = await _client.GetFromJsonAsync<List<DepartmentDto>>("/api/departments/");

        Assert.NotNull(departments);
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
    }

    [Fact]
    public async Task Get_department_returns_not_found_when_handler_returns_null()
    {
        var response = await _client.GetAsync("/api/departments/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_department_returns_created_with_location_and_id()
    {
        _factory.Departments.CreatedId = 17;

        var response = await _client.PostAsJsonAsync(
            "/api/departments/",
            new CreateDepartmentCommand("ENG", "Engineering"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("/api/departments/17", response.Headers.Location?.OriginalString);
        Assert.Equal(new CreateDepartmentCommand("ENG", "Engineering"), _factory.Departments.LastCreate);

        var body = await response.Content.ReadFromJsonAsync<CreatedIdResponse>();
        Assert.Equal(17, body?.Id);
    }

    [Fact]
    public async Task Update_department_uses_route_id_and_returns_no_content()
    {
        var response = await _client.PutAsJsonAsync(
            "/api/departments/5",
            new UpdateDepartmentCommand(999, "BIO", "Biology"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(new UpdateDepartmentCommand(5, "BIO", "Biology"), _factory.Departments.LastUpdate);
    }

    [Fact]
    public async Task Delete_department_returns_not_found_when_handler_returns_false()
    {
        _factory.Departments.DeleteResult = false;

        var response = await _client.DeleteAsync("/api/departments/404");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(new DeleteDepartmentCommand(404), _factory.Departments.LastDelete);
    }

    private sealed record CreatedIdResponse(int Id);
}
