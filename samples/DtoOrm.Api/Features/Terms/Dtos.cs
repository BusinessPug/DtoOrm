namespace DtoOrm.Api.Features.Terms;

public sealed record TermDto(int Id, string Code, string Name, DateOnly StartDate, DateOnly EndDate);
