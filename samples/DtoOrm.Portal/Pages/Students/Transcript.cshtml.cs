using DtoOrm.Portal.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DtoOrm.Portal.Pages.Students;

public sealed class TranscriptModel : ApiPageModel
{
    private static readonly IReadOnlyDictionary<string, double> GradePoints = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
    {
        ["A+"] = 4.0, ["A"] = 4.0, ["A-"] = 3.7,
        ["B+"] = 3.3, ["B"] = 3.0, ["B-"] = 2.7,
        ["C+"] = 2.3, ["C"] = 2.0, ["C-"] = 1.7,
        ["D+"] = 1.3, ["D"] = 1.0, ["F"] = 0.0,
    };

    private readonly ISchoolApiClient _api;

    public TranscriptModel(ISchoolApiClient api) => _api = api;

    public StudentItem? Student { get; private set; }
    public IReadOnlyList<TranscriptItem> Entries { get; private set; } = Array.Empty<TranscriptItem>();

    public int TotalCredits => Entries.Where(e => e.IsGraded).Sum(e => e.Credits);
    public int InProgressCredits => Entries.Where(e => !e.IsGraded).Sum(e => e.Credits);

    public double? Gpa
    {
        get
        {
            var graded = Entries
                .Where(e => e.Grade is not null && GradePoints.ContainsKey(e.Grade))
                .ToList();
            var credits = graded.Sum(e => e.Credits);
            if (credits == 0)
            {
                return null;
            }

            var weighted = graded.Sum(e => GradePoints[e.Grade!] * e.Credits);
            return Math.Round(weighted / credits, 2);
        }
    }

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        var reached = await TryAsync(async () =>
        {
            Student = await _api.GetStudentAsync(id, cancellationToken);
            if (Student is not null)
            {
                Entries = await _api.GetStudentTranscriptAsync(id, cancellationToken);
            }
        });

        if (reached && Student is null)
        {
            return NotFound();
        }

        return Page();
    }
}
