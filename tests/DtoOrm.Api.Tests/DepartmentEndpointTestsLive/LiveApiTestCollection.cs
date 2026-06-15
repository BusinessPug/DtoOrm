using Xunit;

namespace DtoOrm.Api.Tests.DepartmentEndpointTestsLive;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class LiveApiTestCollection : ICollectionFixture<LiveApiFixture>
{
    public const string Name = "Live API department tests";
}
