namespace DtoOrm.Portal.Services;

/// <summary>
/// Read access to the DtoOrm School API. Page models depend on this abstraction rather than on
/// <see cref="System.Net.Http.HttpClient"/> directly, keeping the UI testable and decoupled from
/// transport details.
/// </summary>
public interface ISchoolApiClient
{
    Task<IReadOnlyList<DepartmentCatalogItem>> GetDepartmentCatalogAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PopularCourseItem>> GetPopularCoursesAsync(int minEnrollments = 1, int take = 20, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OfferingSeatsItem>> GetOfferingSeatsAsync(int? termId = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StudentItem>> GetStudentsAsync(string? lastNameLike = null, bool? isActive = null, int take = 100, CancellationToken cancellationToken = default);

    Task<StudentItem?> GetStudentAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TranscriptItem>> GetStudentTranscriptAsync(int studentId, CancellationToken cancellationToken = default);

    Task<OfferingDetailsItem?> GetOfferingDetailsAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RosterItem>> GetOfferingRosterAsync(int offeringId, CancellationToken cancellationToken = default);
}
