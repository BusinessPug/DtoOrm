using DtoOrm.Api.Application.Common;

namespace DtoOrm.Api.Features.Offerings;

public sealed record CreateOfferingCommand(int CourseId, int TeacherId, int TermId, int Capacity, string Room, string? Notes = null) : ICommand<int>;
public sealed record UpdateOfferingCommand(int Id, int CourseId, int TeacherId, int TermId, int Capacity, string Room, string? Notes = null) : ICommand<bool>;
public sealed record DeleteOfferingCommand(int Id) : ICommand<bool>;
