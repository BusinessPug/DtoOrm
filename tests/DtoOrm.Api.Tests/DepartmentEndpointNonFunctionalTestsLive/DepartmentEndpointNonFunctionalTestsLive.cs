using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using DtoOrm.Api.Features.Departments;
using DtoOrm.Api.Tests.DepartmentEndpointTestsLive;
using Xunit;

namespace DtoOrm.Api.Tests.DepartmentEndpointNonFunctionalTestsLive;

[Collection(LiveApiTestCollection.Name)]
public sealed class DepartmentEndpointNonFunctionalTestsLive
{
    private static readonly TimeSpan SingleInteractionBudget = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ReadConsistencyBudget = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan MutationLifecycleBudget = TimeSpan.FromSeconds(20);

    private readonly LiveApiFixture _fixture;

    public DepartmentEndpointNonFunctionalTestsLive(LiveApiFixture fixture) => _fixture = fixture;

    [Fact]
    [Trait("Category", "NonFunctional")]
    public async Task Read_interactions_are_fast_and_consistent_across_repeated_live_calls()
    {
        using var client = _fixture.CreateClient();

        var scenario = await MeasureAsync(async () =>
        {
            for (var attempt = 0; attempt < 3; attempt++)
            {
                var departments = await AssertCompletesWithinAsync(
                    () => client.GetFromJsonAsync<List<DepartmentDto>>("/api/departments/"),
                    SingleInteractionBudget,
                    $"list departments attempt {attempt + 1}");

                Assert.NotNull(departments);
                Assert.Contains(departments, department => department.Code == "CS" && department.Name == "Computer Science");
                Assert.Contains(departments, department => department.Code == "MATH" && department.Name == "Mathematics");

                var missing = await AssertCompletesWithinAsync(
                    () => client.GetAsync("/api/departments/2147483647"),
                    SingleInteractionBudget,
                    $"missing department attempt {attempt + 1}");

                Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
            }
        });

        Assert.True(
            scenario <= ReadConsistencyBudget,
            $"Read consistency scenario took {scenario.TotalMilliseconds:N0} ms; budget is {ReadConsistencyBudget.TotalMilliseconds:N0} ms.");
    }

    [Fact]
    [Trait("Category", "NonFunctional")]
    public async Task Mutation_lifecycle_is_fast_and_consistent_against_live_api()
    {
        using var client = _fixture.CreateClient();
        var rollback = new DepartmentRollback(_fixture.DatabaseConnectionString);
        var originalCode = NewDepartmentCode();
        var updatedCode = NewDepartmentCode();
        rollback.TrackCode(originalCode);
        rollback.TrackCode(updatedCode);

        try
        {
            var scenario = await MeasureAsync(async () =>
            {
                var createResponse = await AssertCompletesWithinAsync(
                    () => client.PostAsJsonAsync(
                        "/api/departments/",
                        new CreateDepartmentCommand(originalCode, "Live Nonfunctional Department")),
                    SingleInteractionBudget,
                    "create department");

                Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

                var created = await createResponse.Content.ReadFromJsonAsync<CreatedIdResponse>();
                Assert.NotNull(created);
                rollback.Track(created.Id, originalCode);

                Assert.Equal($"/api/departments/{created.Id}", createResponse.Headers.Location?.OriginalString);

                var fetchedAfterCreate = await AssertCompletesWithinAsync(
                    () => client.GetFromJsonAsync<DepartmentDto>($"/api/departments/{created.Id}"),
                    SingleInteractionBudget,
                    "fetch created department");

                Assert.NotNull(fetchedAfterCreate);
                Assert.Equal(created.Id, fetchedAfterCreate.Id);
                Assert.Equal(originalCode, fetchedAfterCreate.Code);
                Assert.Equal("Live Nonfunctional Department", fetchedAfterCreate.Name);

                var updateResponse = await AssertCompletesWithinAsync(
                    () => client.PutAsJsonAsync(
                        $"/api/departments/{created.Id}",
                        new UpdateDepartmentCommand(999, updatedCode, "Live Nonfunctional Department Updated")),
                    SingleInteractionBudget,
                    "update department");

                Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);

                var fetchedAfterUpdate = await AssertCompletesWithinAsync(
                    () => client.GetFromJsonAsync<DepartmentDto>($"/api/departments/{created.Id}"),
                    SingleInteractionBudget,
                    "fetch updated department");

                Assert.NotNull(fetchedAfterUpdate);
                Assert.Equal(created.Id, fetchedAfterUpdate.Id);
                Assert.Equal(updatedCode, fetchedAfterUpdate.Code);
                Assert.Equal("Live Nonfunctional Department Updated", fetchedAfterUpdate.Name);

                var deleteResponse = await AssertCompletesWithinAsync(
                    () => client.DeleteAsync($"/api/departments/{created.Id}"),
                    SingleInteractionBudget,
                    "delete department");

                Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

                var getAfterDelete = await AssertCompletesWithinAsync(
                    () => client.GetAsync($"/api/departments/{created.Id}"),
                    SingleInteractionBudget,
                    "fetch deleted department");

                Assert.Equal(HttpStatusCode.NotFound, getAfterDelete.StatusCode);
            });

            Assert.True(
                scenario <= MutationLifecycleBudget,
                $"Mutation lifecycle scenario took {scenario.TotalMilliseconds:N0} ms; budget is {MutationLifecycleBudget.TotalMilliseconds:N0} ms.");
        }
        finally
        {
            await rollback.CleanAsync();
        }
    }

    private static async Task<TimeSpan> MeasureAsync(Func<Task> action)
    {
        var stopwatch = Stopwatch.StartNew();
        await action();
        stopwatch.Stop();

        return stopwatch.Elapsed;
    }

    private static async Task<T> AssertCompletesWithinAsync<T>(
        Func<Task<T>> action,
        TimeSpan budget,
        string operation)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = await action();
        stopwatch.Stop();

        Assert.True(
            stopwatch.Elapsed <= budget,
            $"{operation} took {stopwatch.Elapsed.TotalMilliseconds:N0} ms; budget is {budget.TotalMilliseconds:N0} ms.");

        return result;
    }

    private static string NewDepartmentCode()
        => $"NF{Guid.NewGuid():N}"[..10].ToUpperInvariant();

    private sealed record CreatedIdResponse(int Id);
}
