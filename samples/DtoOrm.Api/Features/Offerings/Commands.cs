using DtoOrm.Api.Application.Common;

namespace DtoOrm.Api.Features.Offerings;

public sealed record CreateOfferingCommand(int CourseId, int TeacherId, int TermId, int Capacity, string Room) : ICommand<int>;
public sealed record UpdateOfferingCommand(int Id, int CourseId, int TeacherId, int TermId, int Capacity, string Room) : ICommand<bool>;
public sealed record DeleteOfferingCommand(int Id) : ICommand<bool>;
