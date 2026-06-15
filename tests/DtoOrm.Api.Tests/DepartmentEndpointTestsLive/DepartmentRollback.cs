using MySqlConnector;

namespace DtoOrm.Api.Tests.DepartmentEndpointTestsLive;

public sealed class DepartmentRollback
{
    private readonly string _connectionString;
    private readonly HashSet<int> _ids = [];
    private readonly HashSet<string> _codes = [];

    public DepartmentRollback(string connectionString) => _connectionString = connectionString;

    public void Track(int id, string code)
    {
        _ids.Add(id);
        _codes.Add(code);
    }

    public void TrackCode(string code) => _codes.Add(code);

    public async Task CleanAsync()
    {
        if (_ids.Count == 0 && _codes.Count == 0)
            return;

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = connection.CreateCommand();

        var predicates = new List<string>();

        if (_ids.Count > 0)
        {
            var idNames = _ids.Select((_, index) => $"@id{index}").ToArray();
            predicates.Add($"id IN ({string.Join(", ", idNames)})");

            var index = 0;
            foreach (var id in _ids)
                command.Parameters.AddWithValue(idNames[index++], id);
        }

        if (_codes.Count > 0)
        {
            var codeNames = _codes.Select((_, index) => $"@code{index}").ToArray();
            predicates.Add($"code IN ({string.Join(", ", codeNames)})");

            var index = 0;
            foreach (var code in _codes)
                command.Parameters.AddWithValue(codeNames[index++], code);
        }

        command.CommandText = $"DELETE FROM departments WHERE {string.Join(" OR ", predicates)}";
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }
}
