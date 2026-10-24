using Indlela.Data;
using Indlela.Models;
using Indlela.Services.Dashboard;
using Indlela.Services.Progress;
using Indlela.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Indlela.Controllers;

[Authorize]
public class ProgressionController(
    IndlelaDbContext db,
    IProgressService progress,
    IDashboardService dashboard,
    Indlela.Services.Career.ICareerService career,
    UserManager<ApplicationUser> users) : Controller
{
    // ---------- UC-18 + UC-31: "Indlela Path" dashboard (design 2) ----------
    [HttpGet("/dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken ct)
    {
        var userId = users.GetUserId(User)!;

        // New learners answer the Smart Start questions first, so their dashboard has a roadmap.
        if (await career.GetProfileAsync(userId, ct) is null) return Redirect("/start");

        var vm = await dashboard.BuildAsync(userId, ct);
        ViewData["Forecast"] = await career.ForecastAsync(userId, ct);
        return View(vm);
    }

    // ---------- UC-17: learner milestone update ----------
    /// <summary>
    /// Learner-initiated updates are always <see cref="ProgressSource.SelfReported"/> and only allowed on external
    /// courses without live sync. Verified sources are written server-side: the in-app lesson player / quiz service
    /// (UC-28/29) records <see cref="ProgressSource.InApp"/>, and the sync job/webhook (UC-30) records
    /// <see cref="ProgressSource.ProviderSync"/> — both through <see cref="IProgressService"/>.
    /// Returns JSON for fetch() callers (offline queue) or redirects for normal form posts.
    /// </summary>
    [HttpPost("/progress"), ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProgress([FromForm] UpdateProgressInput input, CancellationToken ct)
    {
        var wantsJson = Request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase);

        if (!ModelState.IsValid)
            return wantsJson ? ValidationProblem(ModelState) : RedirectWithFlash("Please enter a milestone name and a percentage from 0 to 100.");

        var userId = users.GetUserId(User)!;

        var mode = await db.LearnerEnrollments
            .Where(e => e.Id == input.EnrollmentId && e.UserId == userId)
            .Select(e => (DeliveryMode?)e.Course!.DeliveryMode)
            .SingleOrDefaultAsync(ct);
        if (mode is null) return NotFound();

        if (mode == DeliveryMode.InApp)
        {
            const string msg = "In-app courses record progress automatically as you complete lessons and quizzes.";
            return wantsJson ? Conflict(new { outcome = "InAppIsAutomatic", message = msg }) : RedirectWithFlash(msg);
        }

        var result = await progress.RecordAsync(
            input.EnrollmentId, userId, input.ModuleOrLessonName, input.Percentage, ProgressSource.SelfReported, ct);

        var message = result.Outcome switch
        {
            ProgressOutcome.Recorded when result.Enrollment!.IsCompleted => "Course marked complete. Well done!",
            ProgressOutcome.Recorded => $"Progress saved: {result.Enrollment!.CurrentPercentage}%.",
            ProgressOutcome.Duplicate => "That milestone is already recorded.",
            ProgressOutcome.SelfReportNotAllowed => "This course syncs live from the provider, so progress updates itself.",
            ProgressOutcome.NotEnrolled => "Confirm that you finished registering before logging progress.",
            _ => "We couldn't save that update."
        };

        if (result.NewBadges is { Count: > 0 } newBadges)
            message += " New badge: " + string.Join(", ", newBadges.Select(b => b.Name)) + ".";

        if (wantsJson)
        {
            return result.Outcome == ProgressOutcome.NotFound
                ? NotFound()
                : Ok(new
                {
                    outcome = result.Outcome.ToString(),
                    percentage = result.Enrollment?.CurrentPercentage,
                    completed = result.Enrollment?.IsCompleted ?? false,
                    newBadges = result.NewBadges?.Select(b => b.Name) ?? Enumerable.Empty<string>(),
                    message
                });
        }

        return result.Outcome == ProgressOutcome.NotFound ? NotFound() : RedirectWithFlash(message);
    }

    private RedirectToActionResult RedirectWithFlash(string message)
    {
        TempData["Flash"] = message;
        return RedirectToAction(nameof(Dashboard));
    }
}
