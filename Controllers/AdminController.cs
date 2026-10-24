using Indlela.Data;
using Indlela.Models;
using Indlela.Services.Ingestion;
using Indlela.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Indlela.Controllers;

/// <summary>Admin area: import courses from a link (UC-08/09/10 fast path), review and publish them (UC-11), check links.</summary>
[Authorize(Roles = SeedData.AdminRole)]
[Route("admin")]
public class AdminController(
    IndlelaDbContext db,
    ICourseImportService importer,
    ILinkChecker linkChecker,
    IMemoryCache cache,
    ILogger<AdminController> logger) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var pending = await db.Courses.AsNoTracking()
            .Where(c => !c.IsPublished)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new AdminCourseRowVm(c.Id, c.Title, c.Provider, c.OriginalUrl, c.Category, c.CreatedAt, c.LastVerifiedAt))
            .ToListAsync(ct);

        var flagged = await db.Courses.AsNoTracking()
            .Where(c => c.IsPublished && c.IsBeingReverified)
            .OrderBy(c => c.Title)
            .Select(c => new AdminCourseRowVm(c.Id, c.Title, c.Provider, c.OriginalUrl, c.Category, c.CreatedAt, c.LastVerifiedAt))
            .ToListAsync(ct);

        return View(new AdminQueueVm
        {
            Pending = pending,
            Flagged = flagged,
            PublishedCount = await db.Courses.CountAsync(c => c.IsPublished, ct)
        });
    }

    // ---------- Import a course from a link ----------
    [HttpPost("import"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(string url, CancellationToken ct)
    {
        var result = await importer.ImportFromUrlAsync(url, ct);
        TempData["Flash"] = result.Message;
        TempData["FlashError"] = result.Succeeded ? null : "true";

        return result is { Succeeded: true, CourseId: int id }
            ? RedirectToAction(nameof(Review), new { id })
            : RedirectToAction(nameof(Index));
    }

    // ---------- UC-11: review ----------
    [HttpGet("review/{id:int}")]
    public async Task<IActionResult> Review(int id, CancellationToken ct)
    {
        var c = await db.Courses.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return NotFound();

        return View(new CourseReviewVm
        {
            Id = c.Id, Title = c.Title, Provider = c.Provider, Description = c.Description, Category = c.Category,
            NqfLevel = c.NqfLevel, Credits = c.Credits, AccreditationBody = c.AccreditationBody, IsAccredited = c.IsAccredited,
            DeliveryMode = c.DeliveryMode, LicenceType = c.LicenceType, Language = c.Language, IsFree = c.IsFree,
            EstimatedHours = c.EstimatedHours, OriginalUrl = c.OriginalUrl, IsPublished = c.IsPublished, CreatedAt = c.CreatedAt
        });
    }

    /// <param name="decision">"save", "publish", "unpublish" or "reject".</param>
    [HttpPost("review/{id:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(int id, CourseReviewVm model, string decision, CancellationToken ct)
    {
        var course = await db.Courses.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (course is null) return NotFound();
        model.OriginalUrl = course.OriginalUrl;
        model.IsPublished = course.IsPublished;
        model.CreatedAt = course.CreatedAt;

        if (decision == "reject")
        {
            if (course.IsPublished || await db.LearnerEnrollments.AnyAsync(e => e.CourseId == id, ct))
            {
                TempData["Flash"] = "Published courses with learners can't be deleted — unpublish it instead.";
                TempData["FlashError"] = "true";
                return RedirectToAction(nameof(Review), new { id });
            }
            db.Courses.Remove(course);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Admin {Admin} rejected imported course {CourseId} ({Url})", User.Identity?.Name, id, course.OriginalUrl);
            TempData["Flash"] = $"Rejected and removed “{course.Title}”.";
            return RedirectToAction(nameof(Index));
        }

        // Business rules (UC-11, UC-27)
        if (model.DeliveryMode == DeliveryMode.InApp && !model.LicenceAllowsInApp)
            ModelState.AddModelError(nameof(model.DeliveryMode),
                "Learn here is only allowed for openly licensed (Creative Commons) courses or with a written agreement. Choose On provider, or tick the agreement box.");
        if (decision == "publish" && !model.IsFree)
            ModelState.AddModelError(nameof(model.IsFree), "Indlela only lists free courses. Reject this course if it isn't free.");
        if (model.IsAccredited && (model.NqfLevel is null || string.IsNullOrWhiteSpace(model.AccreditationBody)))
            ModelState.AddModelError(nameof(model.IsAccredited), "An accredited course needs both the NQF level and the accreditation body, as stated on the source page.");

        if (!ModelState.IsValid) return View(model);

        course.Title = model.Title.Trim();
        course.Provider = model.Provider.Trim();
        course.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
        course.Category = model.Category;
        course.NqfLevel = model.NqfLevel;
        course.Credits = model.Credits;
        course.AccreditationBody = string.IsNullOrWhiteSpace(model.AccreditationBody) ? null : model.AccreditationBody.Trim();
        course.IsAccredited = model.IsAccredited;
        course.DeliveryMode = model.DeliveryMode;
        course.LicenceType = string.IsNullOrWhiteSpace(model.LicenceType) ? null : model.LicenceType.Trim();
        course.Language = string.IsNullOrWhiteSpace(model.Language) ? "English" : model.Language.Trim();
        course.IsFree = model.IsFree;
        course.EstimatedHours = model.EstimatedHours;

        switch (decision)
        {
            case "publish":
                course.IsPublished = true;
                course.LastVerifiedAt = DateTime.UtcNow;
                course.IsBeingReverified = false;
                break;
            case "unpublish":
                course.IsPublished = false;
                break;
        }

        await db.SaveChangesAsync(ct);
        cache.Remove("catalog:facets"); // new providers/categories show in the filters straight away

        logger.LogInformation("Admin {Admin} {Decision} course {CourseId}", User.Identity?.Name, decision, id);
        TempData["Flash"] = decision switch
        {
            "publish" => $"Published “{course.Title}”. It's now live in Explore.",
            "unpublish" => $"Unpublished “{course.Title}”. It's back in the review queue.",
            _ => "Saved."
        };
        return decision == "publish" ? RedirectToAction(nameof(Index)) : RedirectToAction(nameof(Review), new { id });
    }

    // ---------- Link check on demand ----------
    [HttpPost("check-links"), ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckLinks(CancellationToken ct)
    {
        var s = await linkChecker.CheckAsync(maxCourses: 60, olderThan: TimeSpan.Zero, ct);
        TempData["Flash"] = $"Checked {s.Checked} course links: {s.Ok} working, {s.Broken} broken, {s.Skipped} couldn't be checked right now.";
        return RedirectToAction(nameof(Index));
    }
}
