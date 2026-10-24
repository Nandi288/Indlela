using Indlela.Data;
using Indlela.Models;
using Indlela.Services.TechPulse;
using Indlela.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace Indlela.Services.Dashboard;

public interface IDashboardService
{
    /// <summary>Builds the "Indlela Path" learner dashboard (UC-18, UC-17, UC-31, UC-24).</summary>
    Task<PathDashboardVm> BuildAsync(string userId, CancellationToken ct = default);
}

public sealed class DashboardService(IndlelaDbContext db, ITechPulseService techPulse, ILogger<DashboardService> logger)
    : IDashboardService
{
    /// <summary>Where each bead sits along the drawn path, as a percentage of its length (see Dashboard.cshtml).</summary>
    public static readonly int[] StopPositions = [0, 28, 55, 84];
    private const int MaxStops = 4;
    private const int MaxBeadsPerWeek = 6;
    private static readonly TimeZoneInfo Sast = FindSast();

    public async Task<PathDashboardVm> BuildAsync(string userId, CancellationToken ct = default)
    {
        var user = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.FirstName, u.TargetSaqaNqfLevel })
            .SingleAsync(ct);

        // 1. Every enrollment the learner hasn't abandoned, with the course fields the page shows.
        var rows = await db.LearnerEnrollments.AsNoTracking()
            .Where(e => e.UserId == userId && e.HandoffStatus != HandoffStatus.Abandoned)
            .Select(e => new
            {
                Vm = new PathCourseVm
                {
                    EnrollmentId = e.Id,
                    CourseId = e.CourseId,
                    Title = e.Course!.Title,
                    Slug = e.Course.Slug,
                    Provider = e.Course.Provider,
                    OriginalUrl = e.Course.OriginalUrl,
                    Category = e.Course.Category,
                    DeliveryMode = e.Course.DeliveryMode,
                    Status = e.HandoffStatus,
                    Percentage = e.CurrentPercentage,
                    LastSource = e.LastProgressSource,
                    HandoffStartedAt = e.HandoffStartedAt,
                    LastActivityAt = e.LastActivityAt,
                    CompletedAt = e.CompletedAt,
                    CompletionSource = e.CompletionSource
                },
                e.Course.Credits
            })
            .ToListAsync(ct);
        var courses = rows.Select(r => r.Vm).ToList();

        // 2. Progress timestamps for the last 8 weeks — enough for the week strip and momentum.
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Sast);
        var weekStart = nowLocal.Date.AddDays(-(((int)nowLocal.DayOfWeek + 6) % 7)); // Monday
        var sinceLocal = weekStart.AddDays(-7 * 7);
        var sinceUtc = TimeZoneInfo.ConvertTimeToUtc(sinceLocal, Sast);
        var stamps = await db.ProgressEntries.AsNoTracking()
            .Where(p => p.Enrollment!.UserId == userId && p.Timestamp >= sinceUtc)
            .Select(p => p.Timestamp)
            .ToListAsync(ct);
        var localStamps = stamps.Select(t => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(t, DateTimeKind.Utc), Sast)).ToList();

        // 3. Badges.
        var earned = await db.BadgeAwards.AsNoTracking()
            .Where(a => a.UserId == userId && !a.IsHidden)
            .OrderBy(a => a.DateAwarded)
            .Select(a => new { a.Badge!.Name, a.Badge.Description, a.DateAwarded, a.IsSelfReportedEvidence, a.VerificationCode })
            .ToListAsync(ct);
        var candidates = await db.Badges.AsNoTracking()
            .Where(b => b.IsActive && !b.Awards.Any(a => a.UserId == userId))
            .Select(b => new { b.Name, b.RuleType, b.RequiredCount, b.RuleCategory, b.IsVerifiedOnly })
            .ToListAsync(ct);

        var active = courses.Where(c => c.IsActive).ToList();
        var completed = courses.Where(c => c.IsCompleted).OrderBy(c => c.CompletedAt).ToList();
        var enrolledForAverage = courses.Where(c => c.Status == HandoffStatus.Enrolled).ToList();

        var (stops, fill) = BuildPath(courses);

        return new PathDashboardVm
        {
            FirstName = user.FirstName,
            GoalLabel = user.TargetSaqaNqfLevel is int nqf ? $"NQF {nqf} goal" : "Your goal",
            Stops = stops,
            PathFillPercent = fill,
            AveragePercent = enrolledForAverage.Count == 0 ? null : (int)Math.Round(enrolledForAverage.Average(c => c.Percentage)),
            UpdatesThisMonth = localStamps.Count(t => t >= nowLocal.AddDays(-30)),
            ContinueCourse = active.OrderByDescending(c => c.LastActivityAt ?? c.HandoffStartedAt).FirstOrDefault(),

            ActiveHere = active.Count(c => c.LearnHere),
            ActiveOnProvider = active.Count(c => !c.LearnHere),
            VerifiedCredits = rows.Where(r => r.Vm.IsCompleted && r.Vm.IsVerified).Sum(r => r.Credits ?? 0),

            ActiveDaysThisWeek = Enumerable.Range(0, 7).Select(d => localStamps.Any(t => t.Date == weekStart.AddDays(d))).ToList(),
            StreakDays = Streak(localStamps, nowLocal.Date),
            Momentum = BuildMomentum(localStamps, weekStart, out var change),
            MomentumChangePercent = change,

            Badges = earned.Select((b, i) => new PathBadgeVm
            {
                Name = b.Name, Description = b.Description, DateAwarded = b.DateAwarded,
                IsSelfReportedEvidence = b.IsSelfReportedEvidence, VerificationCode = b.VerificationCode, Tone = i % 3
            }).ToList(),
            NextBadge = PickNextBadge(),

            // Needs-an-answer first, then active by recent activity, then completed.
            Courses = courses
                .OrderBy(c => c.IsAwaitingConfirmation ? 0 : c.IsActive ? 1 : 2)
                .ThenByDescending(c => c.LastActivityAt ?? c.HandoffStartedAt)
                .ToList(),

            SkillAlertCount = await CountSkillAlertsAsync(courses, ct)
        };

        NextBadgeVm? PickNextBadge()
        {
            if (candidates.Any(b => b.RuleType == BadgeRuleType.FirstProgress) && !courses.Any(c => c.LastSource != null))
                return new NextBadgeVm(candidates.First(b => b.RuleType == BadgeRuleType.FirstProgress).Name, "Log progress on any course", 0);

            return candidates
                .Where(b => b.RuleType == BadgeRuleType.CoursesCompleted)
                .Select(b =>
                {
                    var have = completed.Count(c =>
                        (b.RuleCategory == null || string.Equals(c.Category, b.RuleCategory, StringComparison.OrdinalIgnoreCase)) &&
                        (!b.IsVerifiedOnly || c.IsVerified));
                    var what = b.RuleCategory is null ? "courses" : $"{b.RuleCategory.ToLowerInvariant()} courses";
                    var qualifier = b.IsVerifiedOnly ? " (verified)" : "";
                    return new NextBadgeVm(b.Name, $"{have} of {b.RequiredCount} {what}{qualifier}",
                        (int)Math.Round(100.0 * Math.Min(have, b.RequiredCount) / b.RequiredCount));
                })
                .Where(n => n.Percent < 100)
                .OrderByDescending(n => n.Percent)
                .FirstOrDefault();
        }
    }

    /// <summary>
    /// Picks up to four courses for the path: the most recent completed course(s), then active ones
    /// (furthest along first), then any waiting for sign-up confirmation. The gold line runs to the
    /// furthest bead that has started, or on to the next bead when that one is finished.
    /// </summary>
    private static (IReadOnlyList<PathStopVm> Stops, int Fill) BuildPath(List<PathCourseVm> courses)
    {
        var completed = courses.Where(c => c.IsCompleted).OrderBy(c => c.CompletedAt).ToList();
        var active = courses.Where(c => c.IsActive).OrderByDescending(c => c.Percentage)
            .ThenByDescending(c => c.LastActivityAt).ToList();
        var waiting = courses.Where(c => c.IsAwaitingConfirmation).ToList();

        var completedSlots = completed.Count == 0 ? 0 : Math.Max(1, MaxStops - active.Count - (waiting.Count > 0 ? 1 : 0));
        var picked = completed.TakeLast(Math.Min(completedSlots, completed.Count))
            .Concat(active)
            .Concat(waiting)
            .Take(MaxStops)
            .Select(c => new PathStopVm { Course = c })
            .ToList();

        var lastStarted = picked.FindLastIndex(s => s.IsDone || s.Course.Percentage > 0);
        int fill;
        if (lastStarted < 0) fill = 0;
        else if (picked[lastStarted].IsDone)
            fill = lastStarted + 1 < picked.Count ? StopPositions[lastStarted + 1] : (picked.All(s => s.IsDone) ? 100 : StopPositions[lastStarted]);
        else fill = StopPositions[lastStarted];

        return (picked, fill);
    }

    private static IReadOnlyList<MomentumWeekVm> BuildMomentum(List<DateTime> stamps, DateTime weekStart, out int? changePercent)
    {
        var counts = Enumerable.Range(0, 8)
            .Select(i => weekStart.AddDays(-7 * (7 - i)))
            .Select(start => stamps.Count(t => t >= start && t < start.AddDays(7)))
            .ToList();
        var max = Math.Max(1, counts.Max());

        var previous = counts.Take(4).Sum();
        var recent = counts.Skip(4).Sum();
        changePercent = previous == 0 ? null : (int)Math.Round(100.0 * (recent - previous) / previous);

        return counts.Select((n, i) => new MomentumWeekVm(
            i == 7 ? "Now" : $"W{i + 1}",
            n,
            n == 0 ? 0 : Math.Max(1, (int)Math.Round((double)n / max * MaxBeadsPerWeek)),
            i == 7)).ToList();
    }

    /// <summary>Consecutive days with progress, counting back from today (or yesterday, so the streak survives until tonight).</summary>
    private static int Streak(List<DateTime> stamps, DateTime today)
    {
        var days = stamps.Select(t => t.Date).ToHashSet();
        var day = days.Contains(today) ? today : today.AddDays(-1);
        var streak = 0;
        while (days.Contains(day)) { streak++; day = day.AddDays(-1); }
        return streak;
    }

    private async Task<int> CountSkillAlertsAsync(List<PathCourseVm> courses, CancellationToken ct)
    {
        if (courses.Count == 0) return 0;
        try
        {
            var categories = courses.Select(c => c.Category).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var weekAgo = DateTime.UtcNow.AddDays(-7);
            var snapshot = await techPulse.GetAsync(ct); // cached for 6 hours
            return snapshot.Articles.Count(a => a.PublishedAt >= weekAgo && a.AlignedCourse is { } c && categories.Contains(c.Category));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Tech Pulse unavailable for dashboard skill alerts");
            return 0; // the dashboard never fails because the news feed did
        }
    }

    private static TimeZoneInfo FindSast()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Africa/Johannesburg"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("South Africa Standard Time"); }
    }
}
