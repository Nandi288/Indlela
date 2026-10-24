using Indlela.Data;
using Indlela.Models;
using Indlela.Services.Catalog;
using Indlela.Services.Dashboard;
using Indlela.Services.Roadmaps;
using Indlela.ViewModels;
using Indlela.ViewModels.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Indlela.Controllers.Api;

/// <summary>Bearer scheme name used by the mobile API (same value as IdentityConstants.BearerScheme).</summary>
public static class ApiAuth
{
    public const string Scheme = "Identity.Bearer";
}

// ============================================================================
// Roadmaps — the same rules as /paths on the website (RoadmapService).
// ============================================================================
[ApiController, IgnoreAntiforgeryToken]
[Authorize(AuthenticationSchemes = ApiAuth.Scheme)]
[Route("api/paths")]
public class PathsApiController(IRoadmapService roadmaps, UserManager<ApplicationUser> users) : ControllerBase
{
    private string UserId => users.GetUserId(User)!;

    [HttpGet("")]
    public async Task<IReadOnlyList<PathCardDto>> List(CancellationToken ct) =>
        (await roadmaps.ListAsync(UserId, ct)).Paths.Select(ToDto).ToList();

    [HttpGet("{slug}")]
    public async Task<ActionResult<PathDetailsDto>> Details(string slug, CancellationToken ct)
    {
        var vm = await roadmaps.GetAsync(slug, UserId, ct);
        return vm is null ? NotFound() : ToDto(vm);
    }

    [HttpPost("{slug}/start")]
    public async Task<ActionResult<PathDetailsDto>> Start(string slug, CancellationToken ct)
    {
        if (!await roadmaps.StartAsync(slug, UserId, ct)) return NotFound();
        return ToDto((await roadmaps.GetAsync(slug, UserId, ct))!);
    }

    [HttpPost("steps/{id:int}/done")]
    public async Task<ActionResult<PathDetailsDto>> Done(int id, MarkStepDoneRequest request, CancellationToken ct)
    {
        var result = await roadmaps.MarkDoneAsync(id, UserId, request.EvidenceUrl, ct);
        return result switch
        {
            StepUpdateResult.NotFound => NotFound(),
            StepUpdateResult.BadLink => BadRequest(new ApiError("That link didn't look right. Paste the full address, starting with https://")),
            StepUpdateResult.NotAllowed => BadRequest(new ApiError("Indlela lessons tick themselves off when you pass the quiz.")),
            _ => await ReloadAsync(id, ct)
        };
    }

    [HttpPost("steps/{id:int}/undo")]
    public async Task<ActionResult<PathDetailsDto>> Undo(int id, CancellationToken ct)
    {
        await roadmaps.UndoAsync(id, UserId, ct);
        return await ReloadAsync(id, ct);
    }

    private async Task<ActionResult<PathDetailsDto>> ReloadAsync(int stepId, CancellationToken ct)
    {
        var slug = await roadmaps.SlugForStepAsync(stepId, ct);
        var vm = slug is null ? null : await roadmaps.GetAsync(slug, UserId, ct);
        return vm is null ? NotFound() : ToDto(vm);
    }

    internal static PathCardDto ToDto(PathCardVm p) =>
        new(p.Slug, p.Name, p.Stream, p.Summary, p.Careers, p.StepCount, p.TotalHours, p.DoneCount, p.Percent, p.IsActive, p.HasStarted);

    private static PathDetailsDto ToDto(PathDetailsVm vm) => new(
        vm.Slug, vm.Name, vm.Stream, vm.Summary, vm.Careers, vm.Percent, vm.DoneCount, vm.TotalHours,
        vm.IsActive, vm.OtherActiveName, vm.CompletedAt is not null,
        vm.Steps.Select(s => new PathStepDto(
            s.Id, s.Order, s.Stage, s.Title, s.WhatYouLearn, s.Type.ToString(), s.Status.ToString(), s.Percent,
            s.CourseId, s.ExternalUrl, s.ProviderName, s.CostNote, s.EstimatedHours, s.Skills, s.ProofType,
            s.EvidenceUrl, s.IsEnrolled)).ToList());
}

// ============================================================================
// Course search — the same listing rules as /courses (free, published, link checked in the last 30 days).
// ============================================================================
[ApiController, IgnoreAntiforgeryToken]
[Authorize(AuthenticationSchemes = ApiAuth.Scheme)]
[Route("api/courses")]
public class CoursesApiController(ICourseCatalogService catalog, IndlelaDbContext db, UserManager<ApplicationUser> users) : ControllerBase
{
    [HttpGet("")]
    public async Task<CoursePageDto> Search([FromQuery] string? q, [FromQuery] string? category, [FromQuery] int page = 1, CancellationToken ct = default)
    {
        var filter = new CourseFilterVm
        {
            Q = string.IsNullOrWhiteSpace(q) ? null : q.Trim()[..Math.Min(q.Trim().Length, 100)],
            Category = string.IsNullOrWhiteSpace(category) ? null : category,
            Page = Math.Max(1, page)
        };
        var vm = await catalog.SearchAsync(filter, ct);
        return new CoursePageDto(vm.Courses.Select(ToDto).ToList(), vm.Filter.Page, vm.TotalPages, vm.TotalCount, vm.Categories);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CourseDetailsDto>> Details(int id, CancellationToken ct)
    {
        var c = await db.Courses.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.IsPublished, ct);
        if (c is null) return NotFound();

        var userId = users.GetUserId(User);
        var enrollment = await db.LearnerEnrollments.AsNoTracking()
            .Where(e => e.UserId == userId && e.CourseId == id)
            .OrderByDescending(e => e.CurrentPercentage)
            .FirstOrDefaultAsync(ct);

        var web = $"{Request.Scheme}://{Request.Host}/courses/{c.Id}";
        var original = c.OriginalUrl.StartsWith('/') ? $"{Request.Scheme}://{Request.Host}{c.OriginalUrl}" : c.OriginalUrl;
        return new CourseDetailsDto(
            c.Id, c.Title, c.Provider, c.Category, c.Description, c.NqfLevel, c.IsAccredited, c.AccreditationBody,
            c.DeliveryMode == DeliveryMode.InApp, c.EstimatedHours, c.IsFree, c.LicenceType, original, web,
            enrollment?.CurrentPercentage, enrollment?.HandoffStatus == HandoffStatus.Enrolled);
    }

    private static CourseCardDto ToDto(CourseCardVm c) => new(
        c.Id, c.Title, c.Provider, c.Category, c.NqfLevel, c.IsAccredited, c.AccreditationBody,
        c.DeliveryMode == DeliveryMode.InApp, c.EstimatedHours, c.IsFree);
}

// ============================================================================
// Dashboard + badges — built by the same DashboardService as the website.
// ============================================================================
[ApiController, IgnoreAntiforgeryToken]
[Authorize(AuthenticationSchemes = ApiAuth.Scheme)]
[Route("api/dashboard")]
public class DashboardApiController(IDashboardService dashboard, IRoadmapService roadmaps, UserManager<ApplicationUser> users) : ControllerBase
{
    [HttpGet("")]
    public async Task<DashboardDto> Get(CancellationToken ct)
    {
        var userId = users.GetUserId(User)!;
        var d = await dashboard.BuildAsync(userId, ct);
        var active = (await roadmaps.ListAsync(userId, ct)).Active;

        return new DashboardDto(
            d.FirstName, d.AveragePercent, d.StreakDays, d.ActiveDaysThisWeek,
            d.ActiveHere, d.ActiveOnProvider, d.VerifiedCredits,
            d.Courses.Select(c => new DashboardCourseDto(c.CourseId, c.Title, c.Provider, c.Percentage, c.ModeLabel, c.IsCompleted, c.IsVerified)).ToList(),
            d.Badges.Select(b => new DashboardBadgeDto(b.Name, b.Description, b.DateAwarded, b.VerificationCode, b.Tone)).ToList(),
            d.NextBadge is { } n ? new NextBadgeDto(n.Name, n.ProgressText, n.Percent) : null,
            active is null ? null : PathsApiController.ToDto(active));
    }
}
