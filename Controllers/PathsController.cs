using Indlela.Models;
using Indlela.Services.Roadmaps;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Indlela.Controllers;

/// <summary>Career roadmaps: browse them, follow one, and tick off external courses and the capstone.</summary>
[Route("paths")]
public class PathsController(IRoadmapService roadmaps, UserManager<ApplicationUser> users) : Controller
{
    // GET /paths — every roadmap
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Roadmaps";
        return View(await roadmaps.ListAsync(users.GetUserId(User), ct));
    }

    // GET /paths/mine — the learner's active roadmap, or the list if they haven't picked one
    [Authorize, HttpGet("mine")]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        var slug = await roadmaps.GetActiveSlugAsync(users.GetUserId(User)!, ct);
        return slug is null ? RedirectToAction(nameof(Index)) : RedirectToAction(nameof(Details), new { slug });
    }

    // GET /paths/software-dev — one roadmap with progress per step (guests can look too)
    [HttpGet("{slug}")]
    public async Task<IActionResult> Details(string slug, CancellationToken ct)
    {
        var vm = await roadmaps.GetAsync(slug, users.GetUserId(User), ct);
        if (vm is null) return NotFound();
        ViewData["Title"] = vm.Name;
        return View(vm);
    }

    // POST /paths/software-dev/start — follow (or switch to) this roadmap
    [Authorize, HttpPost("{slug}/start"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(string slug, CancellationToken ct)
    {
        if (!await roadmaps.StartAsync(slug, users.GetUserId(User)!, ct)) return NotFound();
        TempData["Flash"] = "This is now your roadmap. Progress on any other roadmap is kept.";
        return RedirectToAction(nameof(Details), new { slug });
    }

    // POST /paths/steps/12/done — external course or capstone finished
    [Authorize, HttpPost("steps/{id:int}/done"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Done(int id, string? evidenceUrl, CancellationToken ct)
    {
        var result = await roadmaps.MarkDoneAsync(id, users.GetUserId(User)!, evidenceUrl, ct);
        if (result == StepUpdateResult.NotFound) return NotFound();

        switch (result)
        {
            case StepUpdateResult.BadLink:
                TempData["Flash"] = "That link didn't look right. Paste the full address, starting with https://";
                TempData["FlashError"] = "true";
                break;
            case StepUpdateResult.NotAllowed:
                TempData["Flash"] = "Indlela lessons tick themselves off when you pass the quiz.";
                TempData["FlashError"] = "true";
                break;
            default:
                TempData["Flash"] = "Step done. Nice work — keep going.";
                break;
        }
        return await BackToPath(id, ct);
    }

    // POST /paths/steps/12/undo — ticked by mistake
    [Authorize, HttpPost("steps/{id:int}/undo"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Undo(int id, CancellationToken ct)
    {
        await roadmaps.UndoAsync(id, users.GetUserId(User)!, ct);
        TempData["Flash"] = "Step marked as not done.";
        return await BackToPath(id, ct);
    }

    private async Task<IActionResult> BackToPath(int stepId, CancellationToken ct)
    {
        var slug = await roadmaps.SlugForStepAsync(stepId, ct);
        if (slug is null) return RedirectToAction(nameof(Index));
        return Redirect(Url.Action(nameof(Details), new { slug }) + $"#step-{stepId}");
    }
}
