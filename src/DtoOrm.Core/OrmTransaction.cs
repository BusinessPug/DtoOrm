using System.Data.Common;

namespace DtoOrm.Core;

/// <summary>
/// Represents an active database transaction bound to a single connection. Operations started from
/// this transaction (or from the owning <see cref="OrmSession"/> while it is active) execute on the
/// same connection and are committed or rolled back as a unit.
/// </summary>
/// <remarks>
/// Dispose the transaction when finished. If neither <see cref="CommitAsync"/> nor
/// <see cref="RollbackAsync"/> has been called, disposal rolls the transaction back so partial work
/// is never silently persisted.
/// </remarks>
public sealed class OrmTransaction : IAsyncDisposable
{
    private readonly OrmSession _session;
    private readonly DbConnection _connection;
    private readonly DbTransaction _transaction;
    private bool _completed;
    private bool _disposed;

    internal OrmTransaction(OrmSession session, DbConnection connection, DbTransaction transaction)
    {
        _session = session;
        _connection = connection;
        _transaction = transaction;
    }

    /// <summary>Starts a <c>SELECT</c> against <paramref name="table"/> within this transaction.</summary>
    public FromQueryBuilder From(Table table) => _session.From(table);

    /// <summary>Starts an <c>INSERT</c> into <paramref name="table"/> within this transaction.</summary>
    public InsertBuilder InsertInto(Table table) => _session.InsertInto(table);

    /// <summary>Starts an <c>UPDATE</c> of <paramref name="table"/> within this transaction.</summary>
    public UpdateBuilder Update(Table table) => _session.Update(table);

    /// <summary>Starts a <c>DELETE</c> from <paramref name="table"/> within this transaction.</summary>
    public DeleteBuilder DeleteFrom(Table table) => _session.DeleteFrom(table);

    /// <summary>Executes a non-query command within this transaction.</summary>
    public Task<int> ExecuteAsync(QueryCommand command, CancellationToken cancellationToken = default)
        => _session.ExecuteAsync(command, cancellationToken);

    /// <summary>Executes a scalar command within this transaction.</summary>
    public Task<object?> ExecuteScalarAsync(QueryCommand command, CancellationToken cancellationToken = default)
        => _session.ExecuteScalarAsync(command, cancellationToken);

    /// <summary>Commits all work performed within this transaction.</summary>
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        EnsurePending();
        await _transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        _completed = true;
    }

    /// <summary>Rolls back all work performed within this transaction.</summary>
    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        EnsurePending();
        await _transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        _completed = true;
    }

    private void EnsurePending()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(OrmTransaction));
        if (_completed)
        {
            throw new InvalidOperationException("This transaction has already been committed or rolled back.");
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            if (!_completed)
            {
                try
                {
                    await _transaction.RollbackAsync().ConfigureAwait(false);
                }
                catch
                {
                    // The connection may already be broken; never throw from disposal.
                }
            }
        }
        finally
        {
            _session.ClearAmbient();
            await _transaction.DisposeAsync().ConfigureAwait(false);
            await _connection.DisposeAsync().ConfigureAwait(false);
        }
    }
}
