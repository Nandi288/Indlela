using Indlela.Data;
using Indlela.Models;
using Indlela.Services.Demo;
using Indlela.Services.Handoff;
using Indlela.Services.Progress;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Indlela.Controllers;

public class ProviderSignupVm
{
    public string ProviderName { get; init; } = "";
    public Course? Course { get; init; }
    public int? EnrollmentId { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? Email { get; init; }
    public bool Prefilled { get; init; }
    public IReadOnlyList<string> PrefilledFields { get; init; } = [];
}

public class ProviderCourseVm
{
    public required Course Course { get; init; }
    public LearnerEnrollment? Enrollment { get; init; }
    public string[] Modules { get; init; } = [];
    public int DoneCount { get; init; }
}

/// <summary>
/// A SIMULATED external provider website for the demo — how an outsourced learning site looks and behaves:
/// its own sign-up form (pre-filled by Indlela's deep link, UC-14), its own course page, and module completions
/// that notify Indlela (standing in for a provider webhook / xAPI statement, UC-30). Runs only in demo mode.
/// </summary>
[Authorize]
[Route("demo-provider")]
public class DemoProviderController(
    IndlelaDbContext db,
    IHandoffService handoff,
    IProgressService progress,
    UserManager<ApplicationUser> users,
    IWebHostEnvironment env,
    IConfiguration config) : Controller
{
    // ---------- The provider's registration form ----------
    [HttpGet("signup")]
    public async Task<IActionResult> Signup(string? provider, string? first_name, string? last_name, string? email, CancellationToken ct)
    {
        if (!DemoMode.IsOn(config, env)) return NotFound();
        var providerName = await ProviderNameAsync(provider, ct);
        if (providerName is null) return NotFound();

        var userId = users.GetUserId(User)!;
        var enrollment = await db.LearnerEnrollments.AsNoTracking().Include(e => e.Course)
            .Where(e => e.UserId == userId && e.Course!.Provider == providerName && e.Course.DeliveryMode == DeliveryMode.External)
            .OrderByDescending(e => e.HandoffStartedAt)
            .FirstOrDefaultAsync(ct);

        var prefilled = new List<string>();
        if (!string.IsNullOrWhiteSpace(first_name)) prefilled.Add("first name");
        if (!string.IsNullOrWhiteSpace(last_name)) prefilled.Add("surname");
        if (!string.IsNullOrWhiteSpace(email)) prefilled.Add("email");

        return View(new ProviderSignupVm
        {
            ProviderName = providerName, Course = enrollment?.Course, EnrollmentId = enrollment?.Id,
            FirstName = first_name, LastName = last_name, Email = email,
            Prefilled = prefilled.Count > 0, PrefilledFields = prefilled
        });
    }

    /// <summary>The learner presses the provider's Submit. With a linked account (UC-33) the provider confirms the enrolment back to Indlela.</summary>
    [HttpPost("signup"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SignupSubmit(int enrollmentId, CancellationToken ct)
    {
        if (!DemoMode.IsOn(config, env)) return NotFound();
        var enrollment = await handoff.ConfirmAsync(users.GetUserId(User)!, enrollmentId, enrolled: true, null, null, ct);
        if (enrollment is null) return NotFound();
        var slug = await db.Courses.Where(c => c.Id == enrollment.CourseId).Select(c => c.Slug).SingleAsync(ct);
        TempData["ProviderFlash"] = "Account created — you're registered. Indlela was notified and now shows you as enrolled.";
        return Redirect($"/demo-provider/courses/{slug}");
    }

    // ---------- The provider's course page ----------
    [HttpGet("courses/{slug}")]
    public async Task<IActionResult> Course(string slug, CancellationToken ct)
    {
        if (!DemoMode.IsOn(config, env)) return NotFound();
        var course = await db.Courses.AsNoTracking().SingleOrDefaultAsync(c => c.Slug == slug && c.DeliveryMode == DeliveryMode.External, ct);
        if (course is null) return NotFound();
        var userId = users.GetUserId(User);
        var enrollment = await db.LearnerEnrollments.AsNoTracking()
            .SingleOrDefaultAsync(e => e.CourseId == course.Id && e.UserId == userId, ct);

        var modules = ModulesFor(course);
        var done = enrollment is null ? 0 : Math.Min(modules.Length, (int)Math.Floor(enrollment.CurrentPercentage * modules.Length / 100.0 + 0.001));
        return View(new ProviderCourseVm { Course = course, Enrollment = enrollment, Modules = modules, DoneCount = done });
    }

    /// <summary>Finishing a module on the provider sends a progress event to Indlela, tagged Provider sync (verified).</summary>
    [HttpPost("courses/{slug}/complete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteModule(string slug, int module, CancellationToken ct)
    {
        if (!DemoMode.IsOn(config, env)) return NotFound();
        var course = await db.Courses.AsNoTracking().SingleOrDefaultAsync(c => c.Slug == slug, ct);
        if (course is null) return NotFound();
        var userId = users.GetUserId(User)!;
        var enrollment = await db.LearnerEnrollments.AsNoTracking().SingleOrDefaultAsync(e => e.CourseId == course.Id && e.UserId == userId, ct);
        if (enrollment is null) return NotFound();

        var modules = ModulesFor(course);
        if (module < 1 || module > modules.Length) return BadRequest();

        var pct = (int)Math.Round(100.0 * module / modules.Length);
        var result = await progress.RecordAsync(enrollment.Id, userId, $"Module {module}: {modules[module - 1]}", pct, ProgressSource.ProviderSync, ct);

        TempData["ProviderFlash"] = result.Outcome switch
        {
            ProgressOutcome.Recorded when result.Enrollment!.IsCompleted =>
                "Course complete! Indlela received your completion as live, verified progress" +
                (result.NewBadges is { Count: > 0 } b ? $" — and awarded: {string.Join(", ", b.Select(x => x.Name))}." : "."),
            ProgressOutcome.Recorded => $"Module {module} complete. Your progress ({pct}%) was sent to Indlela.",
            ProgressOutcome.NotEnrolled => "Finish registering first.",
            _ => "That module is already recorded."
        };
        return Redirect($"/demo-provider/courses/{slug}");
    }

    internal static string[] ModulesFor(Course c) =>
    [
        $"Welcome to {c.Title}",
        c.Category switch
        {
            "Cloud" => "Compute, storage and networking",
            "Cybersecurity" => "Spotting phishing and scams",
            "Networking" => "IP addresses and subnets",
            "Programming" => "Your first project",
            _ => "Core concepts"
        },
        "Hands-on practice",
        "Final assessment"
    ];

    private async Task<string?> ProviderNameAsync(string? providerSlug, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(providerSlug)) return null;
        var names = await db.ProviderFormMappings.Select(m => m.ProviderName)
            .Union(db.Courses.Select(c => c.Provider)).Distinct().ToListAsync(ct);
        return names.FirstOrDefault(n => DemoSetup.Slug(n) == providerSlug);
    }
}
