using Indlela.Data;
using Indlela.Models;
using Indlela.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace Indlela.Services.Roadmaps;

public enum StepUpdateResult { Ok, NotFound, NotAllowed, BadLink }

public interface IRoadmapService
{
    Task<PathIndexVm> ListAsync(string? userId, CancellationToken ct = default);
    Task<PathDetailsVm?> GetAsync(string slug, string? userId, CancellationToken ct = default);
    Task<string?> GetActiveSlugAsync(string userId, CancellationToken ct = default);
    /// <summary>Follow a roadmap. Any other roadmap becomes inactive; progress on it is kept.</summary>
    Task<bool> StartAsync(string slug, string userId, CancellationToken ct = default);
    /// <summary>Mark an external course or capstone done, with an optional certificate/project link.</summary>
    Task<StepUpdateResult> MarkDoneAsync(int stepId, string userId, string? evidenceUrl, CancellationToken ct = default);
    Task<StepUpdateResult> UndoAsync(int stepId, string userId, CancellationToken ct = default);
    /// <summary>Slug of the roadmap a step belongs to (for redirects).</summary>
    Task<string?> SlugForStepAsync(int stepId, CancellationToken ct = default);
}

public class RoadmapService(IndlelaDbContext db) : IRoadmapService
{
    private static IReadOnlyList<string> Split(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? Array.Empty<string>()
            : value.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public async Task<PathIndexVm> ListAsync(string? userId, CancellationToken ct = default)
    {
        var paths = await db.LearningPaths.AsNoTracking()
            .Where(p => p.IsPublished)
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Name)
            .Select(p => new
            {
                p.Id, p.Slug, p.Name, p.Stream, p.Summary, p.Careers,
                StepCount = p.Steps.Count,
                Hours = p.Steps.Sum(s => s.EstimatedHours ?? 0)
            })
            .ToListAsync(ct);

        var mine = new Dictionary<int, LearnerPath>();
        var doneByPath = new Dictionary<int, int>();
        if (userId is not null)
        {
            mine = await db.LearnerPaths.AsNoTracking().Where(l => l.UserId == userId)
                .ToDictionaryAsync(l => l.LearningPathId, ct);

            foreach (var p in paths)
            {
                var steps = await LoadStepsAsync(p.Id, userId, ct);
                doneByPath[p.Id] = steps.Count(s => s.IsDone);
            }
        }

        return new PathIndexVm
        {
            IsSignedIn = userId is not null,
            Paths = paths.Select(p => new PathCardVm
            {
                Slug = p.Slug, Name = p.Name, Stream = p.Stream, Summary = p.Summary,
                Careers = Split(p.Careers),
                StepCount = p.StepCount, TotalHours = p.Hours,
                DoneCount = doneByPath.GetValueOrDefault(p.Id),
                HasStarted = mine.ContainsKey(p.Id),
                IsActive = mine.TryGetValue(p.Id, out var lp) && lp.IsActive
            }).ToList()
        };
    }

    public async Task<PathDetailsVm?> GetAsync(string slug, string? userId, CancellationToken ct = default)
    {
        var path = await db.LearningPaths.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Slug == slug && p.IsPublished, ct);
        if (path is null) return null;

        var steps = await LoadStepsAsync(path.Id, userId, ct);

        LearnerPath? mine = null;
        string? otherActive = null;
        if (userId is not null)
        {
            mine = await db.LearnerPaths.FirstOrDefaultAsync(l => l.UserId == userId && l.LearningPathId == path.Id, ct);
            if (mine is not { IsActive: true })
            {
                otherActive = await db.LearnerPaths.AsNoTracking()
                    .Where(l => l.UserId == userId && l.IsActive)
                    .Select(l => l.LearningPath!.Name)
                    .FirstOrDefaultAsync(ct);
            }

            // Lessons complete on /learn, so the roadmap notices "finished" the next time it is viewed.
            if (mine is not null && mine.CompletedAt is null && steps.Count > 0 && steps.All(s => s.IsDone))
            {
                mine.CompletedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }
        }

        return new PathDetailsVm
        {
            Id = path.Id, Slug = path.Slug, Name = path.Name, Stream = path.Stream, Summary = path.Summary,
            Careers = Split(path.Careers),
            Steps = steps,
            IsSignedIn = userId is not null,
            IsActive = mine?.IsActive == true,
            OtherActiveName = otherActive,
            CompletedAt = mine?.CompletedAt
        };
    }

    /// <summary>Steps in order with this learner's status: lessons from the enrollment, the rest from LearnerStepProgress.</summary>
    private async Task<List<RoadmapStepVm>> LoadStepsAsync(int pathId, string? userId, CancellationToken ct)
    {
        var steps = await db.RoadmapSteps.AsNoTracking()
            .Where(s => s.LearningPathId == pathId)
            .OrderBy(s => s.Order)
            .ToListAsync(ct);

        var courseIds = steps.Where(s => s.CourseId != null).Select(s => s.CourseId!.Value).ToList();
        var stepIds = steps.Select(s => s.Id).ToList();

        var enrollments = new Dictionary<int, LearnerEnrollment>();
        var progress = new Dictionary<int, LearnerStepProgress>();
        if (userId is not null)
        {
            enrollments = (await db.LearnerEnrollments.AsNoTracking()
                    .Where(e => e.UserId == userId && courseIds.Contains(e.CourseId) && e.HandoffStatus == HandoffStatus.Enrolled)
                    .ToListAsync(ct))
                .GroupBy(e => e.CourseId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.CurrentPercentage).First());

            progress = await db.LearnerStepProgress.AsNoTracking()
                .Where(p => p.UserId == userId && stepIds.Contains(p.RoadmapStepId))
                .ToDictionaryAsync(p => p.RoadmapStepId, ct);
        }

        var result = new List<RoadmapStepVm>(steps.Count);
        foreach (var s in steps)
        {
            LearnerEnrollment? e = null;
            if (s.Type == RoadmapStepType.IndlelaLesson && s.CourseId is int cid) enrollments.TryGetValue(cid, out e);
            progress.TryGetValue(s.Id, out var p);

            var status = s.Type == RoadmapStepType.IndlelaLesson
                ? e is null ? StepStatus.ToDo : e.IsCompleted ? StepStatus.Done : StepStatus.InProgress
                : p is not null ? StepStatus.Done : StepStatus.ToDo;

            result.Add(new RoadmapStepVm
            {
                Id = s.Id, Order = s.Order, Stage = s.Stage, Title = s.Title, WhatYouLearn = s.WhatYouLearn,
                Type = s.Type, CourseId = s.CourseId, ExternalUrl = s.ExternalUrl, ProviderName = s.ProviderName,
                CostNote = s.CostNote, EstimatedHours = s.EstimatedHours, Skills = Split(s.Skills), ProofType = s.ProofType,
                Status = status,
                Percent = s.Type == RoadmapStepType.IndlelaLesson ? e?.CurrentPercentage ?? 0 : p is null ? 0 : 100,
                IsEnrolled = e is not null,
                EvidenceUrl = p?.EvidenceUrl,
                CompletedAt = e?.CompletedAt ?? p?.CompletedAt
            });
        }

        // The first unfinished step is "Next" (unless it's already in progress).
        var next = result.FirstOrDefault(s => !s.IsDone);
        if (next is { Status: StepStatus.ToDo }) next.Status = StepStatus.Next;
        return result;
    }

    public Task<string?> GetActiveSlugAsync(string userId, CancellationToken ct = default) =>
        db.LearnerPaths.AsNoTracking()
            .Where(l => l.UserId == userId && l.IsActive)
            .Select(l => l.LearningPath!.Slug)
            .FirstOrDefaultAsync(ct);

    public async Task<bool> StartAsync(string slug, string userId, CancellationToken ct = default)
    {
        var path = await db.LearningPaths.FirstOrDefaultAsync(p => p.Slug == slug && p.IsPublished, ct);
        if (path is null) return false;

        var mine = await db.LearnerPaths.Where(l => l.UserId == userId).ToListAsync(ct);

        // Deactivate first and save, so the "one active roadmap per learner" index is never broken mid-update.
        var others = mine.Where(l => l.IsActive && l.LearningPathId != path.Id).ToList();
        if (others.Count > 0)
        {
            others.ForEach(l => l.IsActive = false);
            await db.SaveChangesAsync(ct);
        }

        var current = mine.FirstOrDefault(l => l.LearningPathId == path.Id);
        if (current is null)
            db.LearnerPaths.Add(new LearnerPath { UserId = userId, LearningPathId = path.Id, IsActive = true, StartedAt = DateTime.UtcNow });
        else
            current.IsActive = true;

        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<StepUpdateResult> MarkDoneAsync(int stepId, string userId, string? evidenceUrl, CancellationToken ct = default)
    {
        var step = await db.RoadmapSteps.AsNoTracking().FirstOrDefaultAsync(s => s.Id == stepId, ct);
        if (step is null) return StepUpdateResult.NotFound;
        // Indlela lessons are only done when the quiz is passed on /learn.
        if (step.Type == RoadmapStepType.IndlelaLesson) return StepUpdateResult.NotAllowed;

        evidenceUrl = string.IsNullOrWhiteSpace(evidenceUrl) ? null : evidenceUrl.Trim();
        if (evidenceUrl is not null &&
            (evidenceUrl.Length > 500 ||
             !Uri.TryCreate(evidenceUrl, UriKind.Absolute, out var uri) ||
             (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)))
            return StepUpdateResult.BadLink;

        var row = await db.LearnerStepProgress.FirstOrDefaultAsync(p => p.UserId == userId && p.RoadmapStepId == stepId, ct);
        if (row is null)
            db.LearnerStepProgress.Add(new LearnerStepProgress { UserId = userId, RoadmapStepId = stepId, EvidenceUrl = evidenceUrl, CompletedAt = DateTime.UtcNow });
        else if (evidenceUrl is not null)
            row.EvidenceUrl = evidenceUrl;

        await db.SaveChangesAsync(ct);
        return StepUpdateResult.Ok;
    }

    public async Task<StepUpdateResult> UndoAsync(int stepId, string userId, CancellationToken ct = default)
    {
        var row = await db.LearnerStepProgress.FirstOrDefaultAsync(p => p.UserId == userId && p.RoadmapStepId == stepId, ct);
        if (row is null) return StepUpdateResult.NotFound;

        db.LearnerStepProgress.Remove(row);

        // An unfinished roadmap is no longer complete.
        var pathId = await db.RoadmapSteps.Where(s => s.Id == stepId).Select(s => s.LearningPathId).FirstAsync(ct);
        var lp = await db.LearnerPaths.FirstOrDefaultAsync(l => l.UserId == userId && l.LearningPathId == pathId, ct);
        if (lp is not null) lp.CompletedAt = null;

        await db.SaveChangesAsync(ct);
        return StepUpdateResult.Ok;
    }

    public Task<string?> SlugForStepAsync(int stepId, CancellationToken ct = default) =>
        db.RoadmapSteps.AsNoTracking().Where(s => s.Id == stepId).Select(s => s.LearningPath!.Slug).FirstOrDefaultAsync(ct);
}
