using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DtoOrm.Portal.Services;

/// <summary>
/// Typed <see cref="HttpClient"/> implementation of <see cref="ISchoolApiClient"/>. Registered with
/// <c>AddHttpClient</c>, so its <see cref="HttpClient"/> (base address, timeout) is configured centrally
/// in <c>Program.cs</c>.
/// </summary>
public sealed class SchoolApiClient : ISchoolApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;

    public SchoolApiClient(HttpClient http) => _http = http;

    public Task<IReadOnlyList<DepartmentCatalogItem>> GetDepartmentCatalogAsync(CancellationToken cancellationToken = default)
        => GetListAsync<DepartmentCatalogItem>("/api/reports/department-catalog", cancellationToken);

    public Task<IReadOnlyList<PopularCourseItem>> GetPopularCoursesAsync(int minEnrollments = 1, int take = 20, CancellationToken cancellationToken = default)
        => GetListAsync<PopularCourseItem>($"/api/reports/popular-courses?minEnrollments={minEnrollments}&take={take}", cancellationToken);

    public Task<IReadOnlyList<OfferingSeatsItem>> GetOfferingSeatsAsync(int? termId = null, CancellationToken cancellationToken = default)
    {
        var url = termId is null
            ? "/api/reports/offering-seats"
            : $"/api/reports/offering-seats?termId={termId.Value}";
        return GetListAsync<OfferingSeatsItem>(url, cancellationToken);
    }

    public Task<IReadOnlyList<StudentItem>> GetStudentsAsync(string? lastNameLike = null, bool? isActive = null, int take = 100, CancellationToken cancellationToken = default)
    {
        var query = new List<string> { $"take={take}" };
        if (!string.IsNullOrWhiteSpace(lastNameLike))
        {
            query.Add($"lastNameLike={Uri.EscapeDataString(lastNameLike)}");
        }
        if (isActive is not null)
        {
            query.Add($"isActive={(isActive.Value ? "true" : "false")}");
        }

        return GetListAsync<StudentItem>($"/api/students?{string.Join('&', query)}", cancellationToken);
    }

    public Task<StudentItem?> GetStudentAsync(int id, CancellationToken cancellationToken = default)
        => GetOrNullAsync<StudentItem>($"/api/students/{id}", cancellationToken);

    public Task<IReadOnlyList<TranscriptItem>> GetStudentTranscriptAsync(int studentId, CancellationToken cancellationToken = default)
        => GetListAsync<TranscriptItem>($"/api/enrollments/transcript/{studentId}", cancellationToken);

    public Task<OfferingDetailsItem?> GetOfferingDetailsAsync(int id, CancellationToken cancellationToken = default)
        => GetOrNullAsync<OfferingDetailsItem>($"/api/offerings/{id}/details", cancellationToken);

    public Task<IReadOnlyList<RosterItem>> GetOfferingRosterAsync(int offeringId, CancellationToken cancellationToken = default)
        => GetListAsync<RosterItem>($"/api/offerings/{offeringId}/roster", cancellationToken);

    private async Task<IReadOnlyList<T>> GetListAsync<T>(string url, CancellationToken cancellationToken)
    {
        var result = await _http.GetFromJsonAsync<List<T>>(url, JsonOptions, cancellationToken).ConfigureAwait(false);
        return result ?? new List<T>();
    }

    private async Task<T?> GetOrNullAsync<T>(string url, CancellationToken cancellationToken) where T : class
    {
        using var response = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken).ConfigureAwait(false);
    }
}
