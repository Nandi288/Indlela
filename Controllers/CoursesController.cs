using Indlela.Data;
using Indlela.Models;
using Indlela.Services.Catalog;
using Indlela.Services.Handoff;
using Indlela.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Indlela.Controllers;

[Route("courses")]
public class CoursesController(
    IndlelaDbContext db,
    ICourseCatalogService catalog,
    IHandoffService handoff,
    UserManager<ApplicationUser> users,
    ILogger<CoursesController> logger) : Controller
{
    // ---------- UC-04 / UC-05: catalogue with search, filters and server-side paging ----------
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] CourseFilterVm filter, CancellationToken ct)
    {
        if (!ModelState.IsValid) filter = new CourseFilterVm(); // bad query string → plain catalogue, not an error

        var vm = await catalog.SearchAsync(filter, ct);

        // Log a search event on the first page of a real search (Time-to-Discovery KPI). No personal data.
        if (filter.HasAnyFilter && filter.Page == 1)
        {
            var evt = new SearchEvent
            {
                Query = filter.Q?.Trim() is { Length: > 0 } q ? q[..Math.Min(q.Length, 200)] : null,
                Filters = filter.Describe(),
                ResultCount = vm.TotalCount
            };
            db.SearchEvents.Add(evt);
            await db.SaveChangesAsync(ct);
            vm.SearchEventId = evt.Id;
        }

        return View(vm);
    }

    // ---------- UC-07: course details, accreditation and earnable badges ----------
    [HttpGet("{id:int}/{slug?}")]
    public async Task<IActionResult> Details(int id, string? slug, [FromQuery] long? sid, CancellationToken ct)
    {
        var course = await db.Courses.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id && c.IsPublished, ct);
        if (course is null) return NotFound();

        // Canonical URL keeps links shareable and readable.
        if (!string.Equals(slug, course.Slug, StringComparison.Ordinal))
            return RedirectToActionPermanent(nameof(Details), new { id, slug = course.Slug, sid });

        if (sid is long searchId)
        {
            // First click after a search closes the Time-to-Discovery measurement.
            var window = DateTime.UtcNow.AddHours(-1);
            await db.SearchEvents
                .Where(s => s.Id == searchId && s.ClickedAt == null && s.CreatedAt >= window)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.ClickedAt, DateTime.UtcNow)
                    .SetProperty(x => x.ClickedCourseId, id), ct);
        }

        var badges = await db.Badges.AsNoTracking()
            .Where(b => b.IsActive && (b.RuleCategory == null || b.RuleCategory == course.Category))
            .OrderBy(b => b.RequiredCount)
            .Select(b => new BadgePreviewVm
            {
                Id = b.Id, Name = b.Name, Criteria = b.Criteria, IconUrl = b.IconUrl, IsVerifiedOnly = b.IsVerifiedOnly
            })
            .ToListAsync(ct);

        var userId = users.GetUserId(User);
        var enrollment = userId is null
            ? null
            : await db.LearnerEnrollments.AsNoTracking()
                .SingleOrDefaultAsync(e => e.UserId == userId && e.CourseId == id, ct);

        var strategy = course.DeliveryMode == DeliveryMode.External
            ? await db.ProviderFormMappings.AsNoTracking()
                .Where(m => m.ProviderName == course.Provider && m.IsActive)
                .Select(m => (HandoffStrategy?)m.Strategy)
                .SingleOrDefaultAsync(ct) ?? HandoffStrategy.Manual
            : (HandoffStrategy?)null;

        return View(new CourseDetailsVm
        {
            Course = course,
            EarnableBadges = badges,
            MyEnrollment = enrollment,
            IsSignedIn = userId is not null,
            ProviderStrategy = strategy
        });
    }

    // ---------- UC-28 step 1: start an in-app course ----------
    [Authorize, HttpPost("{id:int}/start"), ValidateAntiForgeryToken]
    public async Task<IActionResult> StartLearning(int id, CancellationToken ct)
    {
        var enrollment = await handoff.StartInAppAsync(users.GetUserId(User)!, id, ct);
        if (enrollment is null) return NotFound();

        TempData["Flash"] = "You're enrolled. Every lesson you finish is verified progress on your path.";
        return Redirect($"/learn/{id}");
    }

    // ---------- UC-13 steps 1–2: confirmation panel (nothing shared yet) ----------
    [Authorize, HttpGet("{id:int}/handoff")]
    public async Task<IActionResult> Handoff(int id, CancellationToken ct)
    {
        var learner = await users.GetUserAsync(User);
        if (learner is null) return Challenge();

        var vm = await handoff.PreviewAsync(learner, id, ct);
        return vm is null ? NotFound() : View("HandoffConfirm", vm);
    }

    // ---------- UC-13 steps 3–4 + UC-14: learner confirmed — log it and build the deep link / copy panel ----------
    [Authorize, HttpPost("{id:int}/handoff"), ValidateAntiForgeryToken, ActionName(nameof(Handoff))]
    public async Task<IActionResult> HandoffConfirmed(int id, CancellationToken ct)
    {
        var learner = await users.GetUserAsync(User);
        if (learner is null) return Challenge();

        var result = await handoff.StartAsync(learner, id, ct);
        if (result is null) return NotFound();

        logger.LogInformation("Learner handoff: course {CourseId}, strategy {Strategy}", id, result.Strategy);
        return View("HandoffResult", result);
    }

    // ---------- UC-16: close the feedback loop ----------
    /// <param name="enrolled">true = "Yes, I registered".</param>
    /// <param name="reason">A <see cref="HandoffProblemReason"/> name for "Had a problem"; empty with enrolled=false means "Not yet".</param>
    [Authorize, HttpPost("enrollment/confirm"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmEnrollment(int enrollmentId, bool enrolled, string? reason,
        string? details, CancellationToken ct)
    {
        HandoffProblemReason? parsedReason = null;
        if (!enrolled && !string.IsNullOrWhiteSpace(reason))
        {
            if (!Enum.TryParse<HandoffProblemReason>(reason, ignoreCase: true, out var r) || !Enum.IsDefined(r))
                return BadRequest();
            parsedReason = r;
        }

        var enrollment = await handoff.ConfirmAsync(users.GetUserId(User)!, enrollmentId, enrolled, parsedReason,
            details?.Length > 500 ? details[..500] : details, ct);
        if (enrollment is null) return NotFound();

        TempData["Flash"] = enrollment.HandoffStatus switch
        {
            HandoffStatus.Enrolled => "Great — the course is on your dashboard. Link your provider account for live progress when it's available.",
            HandoffStatus.ProblemReported => "Thanks for telling us. We've logged the problem so the team can fix this provider's sign-up.",
            _ => "No problem. We'll check in with you again in a few days."
        };
        return RedirectToAction("Dashboard", "Progression");
    }
}
