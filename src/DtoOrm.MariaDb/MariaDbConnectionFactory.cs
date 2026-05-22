using System.Data.Common;
using DtoOrm.Core;
using MySqlConnector;

namespace DtoOrm.MariaDb;

public sealed class MariaDbConnectionFactory : IDbConnectionFactory, IAsyncDisposable
{
    private readonly MySqlDataSource _dataSource;

    public MariaDbConnectionFactory(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string cannot be empty.", nameof(connectionString));
        }

        _dataSource = new MySqlDataSourceBuilder(connectionString).Build();
    }

    public async ValueTask<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }

    public ValueTask DisposeAsync() => _dataSource.DisposeAsync();
}
