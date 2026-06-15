using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using DtoOrm.Core;
using DtoOrm.MariaDb;

namespace DtoOrm.Api.Tests.DepartmentTests;

internal sealed class RecordingDatabase
{
    private readonly Queue<CommandResult> _results = new();

    public List<ExecutedDbCommand> Commands { get; } = [];

    public OrmSession CreateSession()
        => new(new RecordingConnectionFactory(this), new MariaDbDialect());

    public void EnqueueReaderRows(params Dictionary<string, object?>[] rows)
        => _results.Enqueue(CommandResult.Reader(rows));

    public void EnqueueScalar(object? value)
        => _results.Enqueue(CommandResult.Scalar(value));

    public void EnqueueNonQuery(int affectedRows)
        => _results.Enqueue(CommandResult.NonQuery(affectedRows));

    private CommandResult DequeueResult(string commandText, FakeParameterCollection parameters)
    {
        var capturedParameters = parameters
            .Items
            .Select(parameter => new ExecutedDbParameter(parameter.ParameterName ?? string.Empty, parameter.Value))
            .ToArray();

        Commands.Add(new ExecutedDbCommand(commandText, capturedParameters));

        if (_results.Count == 0)
        {
            throw new InvalidOperationException($"No queued database result for command:{Environment.NewLine}{commandText}");
        }

        return _results.Dequeue();
    }

    private sealed record CommandResult(
        IReadOnlyList<IReadOnlyDictionary<string, object?>>? Rows,
        object? ScalarValue,
        int? AffectedRows)
    {
        public static CommandResult Reader(IReadOnlyList<IReadOnlyDictionary<string, object?>> rows)
            => new(rows, null, null);

        public static CommandResult Scalar(object? value)
            => new(null, value, null);

        public static CommandResult NonQuery(int affectedRows)
            => new(null, null, affectedRows);
    }

    private sealed class RecordingConnectionFactory : IDbConnectionFactory
    {
        private readonly RecordingDatabase _database;

        public RecordingConnectionFactory(RecordingDatabase database) => _database = database;

        public ValueTask<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
        {
            var connection = new FakeConnection(_database);
            connection.Open();
            return new ValueTask<DbConnection>(connection);
        }
    }

    private sealed class FakeConnection : DbConnection
    {
        private readonly RecordingDatabase _database;
        private ConnectionState _state = ConnectionState.Closed;

        public FakeConnection(RecordingDatabase database) => _database = database;

        [AllowNull]
        public override string ConnectionString { get; set; } = string.Empty;
        public override string Database => "recording";
        public override string DataSource => "recording";
        public override string ServerVersion => "0.0";
        public override ConnectionState State => _state;

        public override void ChangeDatabase(string databaseName) { }
        public override void Close() => _state = ConnectionState.Closed;
        public override void Open() => _state = ConnectionState.Open;

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
            => throw new NotSupportedException();

        protected override DbCommand CreateDbCommand() => new FakeCommand(_database, this);
    }

    private sealed class FakeCommand : DbCommand
    {
        private readonly RecordingDatabase _database;
        private readonly FakeParameterCollection _parameters = new();

        public FakeCommand(RecordingDatabase database, DbConnection connection)
        {
            _database = database;
            DbConnection = connection;
        }

        [AllowNull]
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
            => _database.DequeueResult(CommandText ?? string.Empty, _parameters).AffectedRows
               ?? throw new InvalidOperationException("Queued result was not a non-query result.");

        public override object? ExecuteScalar()
            => _database.DequeueResult(CommandText ?? string.Empty, _parameters).ScalarValue;

        public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken)
            => Task.FromResult(ExecuteNonQuery());

        public override Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken)
            => Task.FromResult(ExecuteScalar());

        protected override DbParameter CreateDbParameter() => new FakeParameter();

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            var rows = _database.DequeueResult(CommandText ?? string.Empty, _parameters).Rows
                       ?? throw new InvalidOperationException("Queued result was not a reader result.");
            return new FakeDataReader(rows);
        }

        protected override Task<DbDataReader> ExecuteDbDataReaderAsync(
            CommandBehavior behavior,
            CancellationToken cancellationToken)
            => Task.FromResult(ExecuteDbDataReader(behavior));
    }

    private sealed class FakeParameter : DbParameter
    {
        public override DbType DbType { get; set; }
        public override ParameterDirection Direction { get; set; }
        public override bool IsNullable { get; set; }
        [AllowNull]
        public override string ParameterName { get; set; } = string.Empty;
        public override int Size { get; set; }
        [AllowNull]
        public override string SourceColumn { get; set; } = string.Empty;
        public override bool SourceColumnNullMapping { get; set; }
        public override object? Value { get; set; }

        public override void ResetDbType() { }
    }

    private sealed class FakeParameterCollection : DbParameterCollection
    {
        private readonly List<DbParameter> _items = [];

        public IReadOnlyList<DbParameter> Items => _items;
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
        public override void CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);
        public override IEnumerator GetEnumerator() => _items.GetEnumerator();
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

    private sealed class FakeDataReader : DbDataReader
    {
        private readonly IReadOnlyList<IReadOnlyDictionary<string, object?>> _rows;
        private readonly IReadOnlyList<string> _columns;
        private int _position = -1;

        public FakeDataReader(IReadOnlyList<IReadOnlyDictionary<string, object?>> rows)
        {
            _rows = rows;
            _columns = rows.Count == 0 ? Array.Empty<string>() : rows[0].Keys.ToArray();
        }

        public override object this[int ordinal] => GetValue(ordinal);
        public override object this[string name] => GetValue(GetOrdinal(name));
        public override int Depth => 0;
        public override int FieldCount => _columns.Count;
        public override bool HasRows => _rows.Count > 0;
        public override bool IsClosed => false;
        public override int RecordsAffected => -1;

        public override bool GetBoolean(int ordinal) => Convert.ToBoolean(GetValue(ordinal));
        public override byte GetByte(int ordinal) => Convert.ToByte(GetValue(ordinal));
        public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => 0;
        public override char GetChar(int ordinal) => Convert.ToChar(GetValue(ordinal));
        public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => 0;
        public override string GetDataTypeName(int ordinal) => GetFieldType(ordinal).Name;
        public override DateTime GetDateTime(int ordinal) => Convert.ToDateTime(GetValue(ordinal));
        public override decimal GetDecimal(int ordinal) => Convert.ToDecimal(GetValue(ordinal));
        public override double GetDouble(int ordinal) => Convert.ToDouble(GetValue(ordinal));
        public override Type GetFieldType(int ordinal) => GetValue(ordinal)?.GetType() ?? typeof(object);
        public override float GetFloat(int ordinal) => Convert.ToSingle(GetValue(ordinal));
        public override Guid GetGuid(int ordinal) => (Guid)GetValue(ordinal);
        public override short GetInt16(int ordinal) => Convert.ToInt16(GetValue(ordinal));
        public override int GetInt32(int ordinal) => Convert.ToInt32(GetValue(ordinal));
        public override long GetInt64(int ordinal) => Convert.ToInt64(GetValue(ordinal));
        public override string GetName(int ordinal) => _columns[ordinal];
        public override int GetOrdinal(string name) => _columns.ToList().FindIndex(column => string.Equals(column, name, StringComparison.OrdinalIgnoreCase));
        public override string GetString(int ordinal) => Convert.ToString(GetValue(ordinal), System.Globalization.CultureInfo.InvariantCulture)!;
        public override object GetValue(int ordinal) => Current[_columns[ordinal]] ?? DBNull.Value;
        public override int GetValues(object[] values)
        {
            var count = Math.Min(values.Length, FieldCount);
            for (var i = 0; i < count; i++)
                values[i] = GetValue(i);
            return count;
        }

        public override bool IsDBNull(int ordinal) => Current[_columns[ordinal]] is null or DBNull;
        public override bool NextResult() => false;
        public override bool Read()
        {
            if (_position + 1 >= _rows.Count)
                return false;

            _position++;
            return true;
        }

        public override Task<bool> ReadAsync(CancellationToken cancellationToken)
            => Task.FromResult(Read());

        public override IEnumerator GetEnumerator()
        {
            while (Read())
                yield return this;
        }

        private IReadOnlyDictionary<string, object?> Current
            => _position >= 0 && _position < _rows.Count
                ? _rows[_position]
                : throw new InvalidOperationException("The reader is not positioned on a row.");
    }
}

internal sealed record ExecutedDbCommand(string Sql, IReadOnlyList<ExecutedDbParameter> Parameters);

internal sealed record ExecutedDbParameter(string Name, object? Value);
