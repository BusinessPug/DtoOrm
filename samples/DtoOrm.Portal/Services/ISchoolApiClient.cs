namespace DtoOrm.Portal.Services;

/// <summary>
/// Read access to the DtoOrm School API. Page models depend on this abstraction rather than on
/// <see cref="System.Net.Http.HttpClient"/> directly, keeping the UI testable and decoupled from
/// transport details.
/// </summary>
public interface ISchoolApiClient
{
    Task<LoginResponse?> LoginAsync(string username, string password, CancellationToken cancellationToken = default);

    Task<CurrentUserItem?> GetCurrentUserAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CourseItem>> GetCoursesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TermItem>> GetTermsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TeacherItem>> GetTeachersAsync(bool? isActive = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DepartmentCatalogItem>> GetDepartmentCatalogAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PopularCourseItem>> GetPopularCoursesAsync(int minEnrollments = 1, int take = 20, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OfferingSeatsItem>> GetOfferingSeatsAsync(int? termId = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StudentItem>> GetStudentsAsync(string? lastNameLike = null, bool? isActive = null, int take = 100, CancellationToken cancellationToken = default);

    Task<StudentItem?> GetStudentAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TranscriptItem>> GetStudentTranscriptAsync(int studentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OfferingItem>> GetOfferingsAsync(int? teacherId = null, int? termId = null, CancellationToken cancellationToken = default);

    Task<OfferingDetailsItem?> GetOfferingDetailsAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RosterItem>> GetOfferingRosterAsync(int offeringId, CancellationToken cancellationToken = default);

    Task<int> CreateOfferingAsync(SaveOfferingRequest request, CancellationToken cancellationToken = default);

    Task UpdateOfferingAsync(int id, SaveOfferingRequest request, CancellationToken cancellationToken = default);

    Task DeleteOfferingAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ScheduleItem>> GetMyScheduleAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ScheduleItem>> GetStudentScheduleAsync(int studentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AcademicBreakItem>> GetAcademicBreaksAsync(CancellationToken cancellationToken = default);
}
