using Indlela.Data;
using Indlela.Models;
using Indlela.Services.Career;
using Indlela.Services.Roadmaps;
using Indlela.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Indlela.Controllers;

/// <summary>Smart Start: six questions after sign-up → a recommended roadmap.</summary>
[Authorize, Route("start")]
public class StartController(ICareerService career, IRoadmapService roadmaps, UserManager<ApplicationUser> users) : Controller
{
    [HttpGet("")]
    public IActionResult Index()
    {
        ViewData["Title"] = "Smart Start";
        return View(new SmartStartInput { HoursPerWeek = 5 });
    }

    [HttpPost(""), ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(SmartStartInput input, CancellationToken ct)
    {
        ViewData["Title"] = "Smart Start";
        if (!ModelState.IsValid) return View(input);

        var result = await career.RecommendAsync(input, ct);
        await career.SaveProfileAsync(users.GetUserId(User)!, input, result.Best.Path.Slug, ct);
        ViewData["Title"] = "Your roadmap";
        return View("Result", result);
    }

    /// <summary>Follow the recommended (or chosen) roadmap and go straight to it.</summary>
    [HttpPost("follow/{slug}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Follow(string slug, CancellationToken ct)
    {
        if (!await roadmaps.StartAsync(slug, users.GetUserId(User)!, ct)) return NotFound();
        TempData["Flash"] = "Your roadmap is ready. Start with step 1.";
        return Redirect($"/paths/{slug}");
    }
}

/// <summary>Skills Portfolio: the learner's own page, its settings, the public link and a printable CV.</summary>
public class PortfolioController(ICareerService career, UserManager<ApplicationUser> users) : Controller
{
    private string BaseUrl => $"{Request.Scheme}://{Request.Host}";

    [Authorize, HttpGet("/portfolio")]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        ViewData["Title"] = "My portfolio";
        var vm = await career.GetPortfolioAsync(users.GetUserId(User)!, isOwner: true, BaseUrl, ct);
        return View("Show", vm);
    }

    [Authorize, HttpPost("/portfolio"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(PortfolioEditInput input, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            TempData["Flash"] = "Some details were too long. Shorten them and try again.";
            TempData["FlashError"] = "true";
            return Redirect("/portfolio");
        }
        await career.SavePortfolioAsync(users.GetUserId(User)!, input, ct);
        TempData["Flash"] = input.IsPublic ? "Portfolio saved. Your public link is live." : "Portfolio saved. It's private until you switch it to public.";
        return Redirect("/portfolio");
    }

    /// <summary>One-page CV to print or save as PDF from the browser (no extra download, light on data).</summary>
    [Authorize, HttpGet("/portfolio/cv")]
    public async Task<IActionResult> Cv(CancellationToken ct)
    {
        var vm = await career.GetPortfolioAsync(users.GetUserId(User)!, isOwner: true, BaseUrl, ct);
        return View("Cv", vm);
    }

    /// <summary>Public portfolio link that learners share with employers. Only works once they make it public.</summary>
    [AllowAnonymous, HttpGet("/p/{slug}")]
    public async Task<IActionResult> Public(string slug, CancellationToken ct)
    {
        var userId = await career.UserIdForSlugAsync(slug, ct);
        if (userId is null) return NotFound();
        var vm = await career.GetPortfolioAsync(userId, isOwner: false, BaseUrl, ct);
        if (vm is null) return NotFound();
        ViewData["Title"] = $"{vm.FullName} · Skills portfolio";
        return View("Show", vm);
    }
}

/// <summary>Job Matching: entry-level roles ranked by how many of their skills the learner has proven.</summary>
public class JobsController(ICareerService career, UserManager<ApplicationUser> users) : Controller
{
    [HttpGet("/jobs")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Jobs";
        var userId = users.GetUserId(User);
        IReadOnlyList<SkillProof> skills = userId is null ? Array.Empty<SkillProof>() : await career.GetSkillsAsync(userId, ct);
        var matches = await career.MatchJobsAsync(skills, ct);
        return View(new JobsPageVm { Matches = matches, SkillCount = skills.Count, IsSignedIn = userId is not null });
    }
}

/// <summary>All badges: earned ones first, then what's left to earn.</summary>
public class BadgesController(IndlelaDbContext db, UserManager<ApplicationUser> users) : Controller
{
    [HttpGet("/badges")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Badges";
        var userId = users.GetUserId(User);
        var badges = await db.Badges.AsNoTracking().Where(b => b.IsActive).OrderBy(b => b.Id).ToListAsync(ct);
        var earned = userId is null ? new Dictionary<int, DateTime>()
            : await db.BadgeAwards.AsNoTracking().Where(a => a.UserId == userId).ToDictionaryAsync(a => a.BadgeId, a => a.DateAwarded, ct);
        var rows = badges.Select(b => new BadgeRowVm(b, earned.TryGetValue(b.Id, out var d) ? d : null))
            .OrderByDescending(r => r.EarnedAt.HasValue).ToList();
        ViewData["SignedIn"] = userId is not null;
        return View(rows);
    }
}

/// <summary>About Us and For Employers (information pages from the top menu).</summary>
public class InfoController : Controller
{
    [HttpGet("/about")]
    public IActionResult About() { ViewData["Title"] = "About us"; return View(); }

    [HttpGet("/employers")]
    public IActionResult Employers() { ViewData["Title"] = "For employers"; return View(); }
}
