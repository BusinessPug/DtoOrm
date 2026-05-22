namespace DtoOrm.Api.Features.Offerings;

public sealed record OfferingDto(
    int Id,
    int CourseId,
    int TeacherId,
    int TermId,
    int Capacity,
    string Room);
