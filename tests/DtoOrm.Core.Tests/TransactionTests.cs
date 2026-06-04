using System.Data;
using System.Data.Common;
using DtoOrm.Core;
using DtoOrm.MariaDb;
using Xunit;

namespace DtoOrm.Core.Tests;

public sealed class TransactionTests
{
    [Fact]
    public async Task Commit_reuses_single_connection_and_commits()
    {
        var factory = new RecordingConnectionFactory();
        await using var session = new OrmSession(factory, new MariaDbDialect());
        var people = new PeopleTable();

        await using (var tx = await session.BeginTransactionAsync())
        {
            await tx.InsertInto(people).Value(people.FirstName, "Ada").ExecuteAsync();
            await tx.Update(people).Set(people.IsActive, true).Where(people.Id.Eq(1)).ExecuteAsync();
            await tx.CommitAsync();
        }

        Assert.Single(factory.Connections);
        var connection = factory.Connections[0];
        Assert.Equal(1, connection.Transactions.Count);
        Assert.True(connection.Transactions[0].Committed);
        Assert.False(connection.Transactions[0].RolledBack);
        Assert.Equal(2, connection.ExecutedNonQueries);
        Assert.True(connection.Disposed);
    }

    [Fact]
    public async Task Dispose_without_commit_rolls_back()
    {
        var factory = new RecordingConnectionFactory();
        await using var session = new OrmSession(factory, new MariaDbDialect());
        var people = new PeopleTable();

        await using (var tx = await session.BeginTransactionAsync())
        {
            await tx.InsertInto(people).Value(people.FirstName, "Grace").ExecuteAsync();
        }

        var transaction = factory.Connections[0].Transactions[0];
        Assert.True(transaction.RolledBack);
        Assert.False(transaction.Committed);
    }

    [Fact]
    public async Task WithTransaction_rolls_back_when_work_throws()
    {
        var factory = new RecordingConnectionFactory();
        await using var session = new OrmSession(factory, new MariaDbDialect());
        var people = new PeopleTable();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await session.WithTransactionAsync(async tx =>
            {
                await tx.InsertInto(people).Value(people.FirstName, "Linus").ExecuteAsync();
                throw new InvalidOperationException("boom");
            }));

        var transaction = factory.Connections[0].Transactions[0];
        Assert.True(transaction.RolledBack);
        Assert.False(transaction.Committed);
    }

    [Fact]
    public async Task Beginning_second_transaction_while_active_throws()
    {
        var factory = new RecordingConnectionFactory();
        await using var session = new OrmSession(factory, new MariaDbDialect());

        await using var tx = await session.BeginTransactionAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.BeginTransactionAsync());
    }

    [Fact]
    public async Task Session_is_usable_again_after_transaction_disposed()
    {
        var factory = new RecordingConnectionFactory();
        await using var session = new OrmSession(factory, new MariaDbDialect());

        await using (var tx = await session.BeginTransactionAsync())
        {
            await tx.CommitAsync();
        }

        // A non-transactional execute should open a fresh connection without throwing.
        var people = new PeopleTable();
        await session.InsertInto(people).Value(people.FirstName, "Margaret").ExecuteAsync();

        Assert.Equal(2, factory.Connections.Count);
    }

    private sealed class PeopleTable : Table
    {
        public PeopleTable() : base("people", "People", "p")
        {
            Id = new Column<int>(this, "id", "Id");
            FirstName = new Column<string>(this, "first_name", "FirstName");
            IsActive = new Column<bool>(this, "is_active", "IsActive");
        }

        public Column<int> Id { get; }
        public Column<string> FirstName { get; }
        public Column<bool> IsActive { get; }
    }

    private sealed class RecordingConnectionFactory : IDbConnectionFactory
    {
        public List<FakeConnection> Connections { get; } = new();

        public ValueTask<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
        {
            var connection = new FakeConnection();
            Connections.Add(connection);
            connection.Open();
            return new ValueTask<DbConnection>(connection);
        }
    }

    private sealed class FakeConnection : DbConnection
    {
        private ConnectionState _state = ConnectionState.Closed;

        public List<FakeTransaction> Transactions { get; } = new();
        public int ExecutedNonQueries { get; private set; }
        public bool Disposed { get; private set; }

        public override string ConnectionString { get; set; } = string.Empty;
        public override string Database => "fake";
        public override string DataSource => "fake";
        public override string ServerVersion => "0.0";
        public override ConnectionState State => _state;

        public override void ChangeDatabase(string databaseName) { }
        public override void Close() => _state = ConnectionState.Closed;
        public override void Open() => _state = ConnectionState.Open;

        internal void RecordNonQuery() => ExecutedNonQueries++;

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        {
            var transaction = new FakeTransaction(this, isolationLevel);
            Transactions.Add(transaction);
            return transaction;
        }

        protected override DbCommand CreateDbCommand() => new FakeCommand(this);

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class FakeTransaction : DbTransaction
    {
        private readonly FakeConnection _connection;

        public FakeTransaction(FakeConnection connection, IsolationLevel isolationLevel)
        {
            _connection = connection;
            IsolationLevel = isolationLevel;
        }

        public bool Committed { get; private set; }
        public bool RolledBack { get; private set; }

        public override IsolationLevel IsolationLevel { get; }
        protected override DbConnection DbConnection => _connection;

        public override void Commit() => Committed = true;
        public override void Rollback() => RolledBack = true;
    }

    private sealed class FakeCommand : DbCommand
    {
        private readonly FakeConnection _connection;
        private readonly FakeParameterCollection _parameters = new();

        public FakeCommand(FakeConnection connection) => _connection = connection;

        public override string CommandText { get; set; } = string.Empty;
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; }
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }
        protected override DbConnection? DbConnection { get; set; }
        protected override DbParameterCollection DbParameterCollection => _parameters;
        protected override DbTransaction? DbTransaction { get; set; }

        public override void Cancel() { }
        public override void Prepare() { }

        public override int ExecuteNonQuery()
        {
            _connection.RecordNonQuery();
            return 1;
        }

        public override object? ExecuteScalar() => 1L;

        protected override DbParameter CreateDbParameter() => new FakeParameter();

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
            => throw new NotSupportedException();
    }

    private sealed class FakeParameter : DbParameter
    {
        public override DbType DbType { get; set; }
        public override ParameterDirection Direction { get; set; }
        public override bool IsNullable { get; set; }
        public override string ParameterName { get; set; } = string.Empty;
        public override int Size { get; set; }
        public override string SourceColumn { get; set; } = string.Empty;
        public override bool SourceColumnNullMapping { get; set; }
        public override object? Value { get; set; }

        public override void ResetDbType() { }
    }

    private sealed class FakeParameterCollection : DbParameterCollection
    {
        private readonly List<DbParameter> _items = new();

        public override int Count => _items.Count;
        public override object SyncRoot { get; } = new();

        public override int Add(object value)
        {
            _items.Add((DbParameter)value);
            return _items.Count - 1;
        }

        public override void AddRange(Array values)
        {
            foreach (var value in values) Add(value);
        }

        public override void Clear() => _items.Clear();
        public override bool Contains(object value) => _items.Contains((DbParameter)value);
        public override bool Contains(string value) => IndexOf(value) >= 0;
        public override void CopyTo(Array array, int index) => ((System.Collections.ICollection)_items).CopyTo(array, index);
        public override System.Collections.IEnumerator GetEnumerator() => _items.GetEnumerator();
        public override int IndexOf(object value) => _items.IndexOf((DbParameter)value);
        public override int IndexOf(string parameterName) => _items.FindIndex(p => p.ParameterName == parameterName);
        public override void Insert(int index, object value) => _items.Insert(index, (DbParameter)value);
        public override void Remove(object value) => _items.Remove((DbParameter)value);
        public override void RemoveAt(int index) => _items.RemoveAt(index);
        public override void RemoveAt(string parameterName) => RemoveAt(IndexOf(parameterName));

        protected override DbParameter GetParameter(int index) => _items[index];
        protected override DbParameter GetParameter(string parameterName) => _items[IndexOf(parameterName)];
        protected override void SetParameter(int index, DbParameter value) => _items[index] = value;
        protected override void SetParameter(string parameterName, DbParameter value) => _items[IndexOf(parameterName)] = value;
    }
}
