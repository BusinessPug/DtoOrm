using Xunit;

namespace DtoOrm.Api.Tests.DepartmentTestsLive;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class LiveDatabaseTestCollection : ICollectionFixture<LiveDatabaseFixture>
{
    public const string Name = "Live database department tests";
}
