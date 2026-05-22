using DtoOrm.Core;

namespace DtoOrm.MariaDb;

public static class MariaDbOrm
{
    public static OrmSession Create(string connectionString)
    {
        var factory = new MariaDbConnectionFactory(connectionString);
        return new OrmSession(factory, new MariaDbDialect(), ownedDisposables: new[] { factory });
    }
}
