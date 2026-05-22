namespace DtoOrm.Core;

public sealed record QueryCommand(string Sql, IReadOnlyList<SqlParameterValue> Parameters);
