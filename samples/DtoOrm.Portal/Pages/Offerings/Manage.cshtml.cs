using System.ComponentModel.DataAnnotations;
using DtoOrm.Portal.Services;
using Microsoft.AspNetCore.Mvc;

namespace DtoOrm.Portal.Pages.Offerings;

public sealed class ManageModel : ApiPageModel
{
    private const int ActiveTermId = 3;
    private readonly ISchoolApiClient _api;

    public ManageModel(ISchoolApiClient api) => _api = api;

    [BindProperty(SupportsGet = true)]
    public int? EditId { get; set; }

    [BindProperty]
    public OfferingForm Input { get; set; } = new();

    public IReadOnlyList<OfferingItem> Offerings { get; private set; } = Array.Empty<OfferingItem>();
    public IReadOnlyList<OfferingDetailsItem> OfferingDetails { get; private set; } = Array.Empty<OfferingDetailsItem>();
    public IReadOnlyList<CourseItem> Courses { get; private set; } = Array.Empty<CourseItem>();
    public IReadOnlyList<TermItem> Terms { get; private set; } = Array.Empty<TermItem>();
    public IReadOnlyList<TeacherItem> Teachers { get; private set; } = Array.Empty<TeacherItem>();

    public bool IsAdmin => User.IsInRole("Administrator");
    public int? CurrentTeacherId => User.GetTeacherId();

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!IsAdmin && !User.IsInRole("Teacher"))
        {
            return Forbid();
        }

        await LoadAsync(cancellationToken);

        if (EditId is not null)
        {
            var offering = Offerings.FirstOrDefault(item => item.Id == EditId.Value);
            if (offering is null)
            {
                return NotFound();
            }

            Input = OfferingForm.From(offering);
        }
        else
        {
            Input.TermId = ActiveTermId;
            Input.TeacherId = CurrentTeacherId ?? Teachers.FirstOrDefault()?.Id ?? 0;
            Input.Capacity = 30;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        if (!IsAdmin && !User.IsInRole("Teacher"))
        {
            return Forbid();
        }

        if (User.IsInRole("Teacher") && CurrentTeacherId is int teacherId)
        {
            Input.TeacherId = teacherId;
        }

        if (!ModelState.IsValid)
        {
            await LoadAsync(cancellationToken);
            return Page();
        }

        var request = new SaveOfferingRequest(
            Input.CourseId,
            Input.TeacherId,
            Input.TermId,
            Input.Capacity,
            Input.Room.Trim(),
            string.IsNullOrWhiteSpace(Input.Notes) ? null : Input.Notes.Trim());

        if (Input.Id is null or 0)
        {
            await _api.CreateOfferingAsync(request, cancellationToken);
        }
        else
        {
            await _api.UpdateOfferingAsync(Input.Id.Value, request, cancellationToken);
        }

        return RedirectToPage("/Offerings/Manage");
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, CancellationToken cancellationToken)
    {
        if (!IsAdmin && !User.IsInRole("Teacher"))
        {
            return Forbid();
        }

        await _api.DeleteOfferingAsync(id, cancellationToken);
        return RedirectToPage("/Offerings/Manage");
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        await TryAsync(async () =>
        {
            var teacherId = IsAdmin ? null : CurrentTeacherId;
            Courses = await _api.GetCoursesAsync(cancellationToken);
            Terms = await _api.GetTermsAsync(cancellationToken);
            Teachers = await _api.GetTeachersAsync(isActive: true, cancellationToken);
            Offerings = await _api.GetOfferingsAsync(teacherId: teacherId, cancellationToken: cancellationToken);

            var details = new List<OfferingDetailsItem>();
            foreach (var offering in Offerings.OrderBy(o => o.TermId).ThenBy(o => o.CourseId))
            {
                var detail = await _api.GetOfferingDetailsAsync(offering.Id, cancellationToken);
                if (detail is not null)
                {
                    details.Add(detail);
                }
            }

            OfferingDetails = details;
        });
    }

    public sealed class OfferingForm
    {
        public int? Id { get; set; }

        [Range(1, int.MaxValue)]
        public int CourseId { get; set; }

        [Range(1, int.MaxValue)]
        public int TeacherId { get; set; }

        [Range(1, int.MaxValue)]
        public int TermId { get; set; }

        [Range(1, 500)]
        public int Capacity { get; set; } = 30;

        [Required]
        [StringLength(50)]
        public string Room { get; set; } = "";

        [StringLength(1000)]
        public string? Notes { get; set; }

        public static OfferingForm From(OfferingItem item) => new()
        {
            Id = item.Id,
            CourseId = item.CourseId,
            TeacherId = item.TeacherId,
            TermId = item.TermId,
            Capacity = item.Capacity,
            Room = item.Room,
            Notes = item.Notes
        };
    }
}
