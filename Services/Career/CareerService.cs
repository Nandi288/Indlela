using System.Text.RegularExpressions;
using Indlela.Data;
using Indlela.Models;
using Indlela.Services.Roadmaps;
using Indlela.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace Indlela.Services.Career;

public interface ICareerService
{
    // Smart Start
    Task<LearnerProfile?> GetProfileAsync(string userId, CancellationToken ct = default);
    Task<SmartStartResultVm> RecommendAsync(SmartStartInput input, CancellationToken ct = default);
    Task SaveProfileAsync(string userId, SmartStartInput input, string recommendedSlug, CancellationToken ct = default);

    // Skills Portfolio + Job Matching
    Task<IReadOnlyList<SkillProof>> GetSkillsAsync(string userId, CancellationToken ct = default);
    Task<IReadOnlyList<JobMatchVm>> MatchJobsAsync(IReadOnlyList<SkillProof> skills, CancellationToken ct = default);
    Task<PortfolioProfile> EnsurePortfolioAsync(string userId, CancellationToken ct = default);
    Task<PortfolioVm?> GetPortfolioAsync(string userId, bool isOwner, string baseUrl, CancellationToken ct = default);
    Task<string?> UserIdForSlugAsync(string slug, CancellationToken ct = default);
    Task SavePortfolioAsync(string userId, PortfolioEditInput input, CancellationToken ct = default);

    // Forecast
    Task<ForecastVm?> ForecastAsync(string userId, CancellationToken ct = default);
}

public class CareerService(IndlelaDbContext db, IRoadmapService roadmaps) : ICareerService
{
    // =====================================================================
    // Smart Start: six answers → a recommended roadmap, with reasons
    // =====================================================================
    public Task<LearnerProfile?> GetProfileAsync(string userId, CancellationToken ct = default) =>
        db.LearnerProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId, ct);

    public async Task<SmartStartResultVm> RecommendAsync(SmartStartInput a, CancellationToken ct = default)
    {
        var paths = (await roadmaps.ListAsync(null, ct)).Paths;
        var hours = Math.Clamp(a.HoursPerWeek, 1, 40);

        var ranked = paths.Select(p =>
        {
            var score = 0;
            var reasons = new List<string>();

            // What they enjoy matters most.
            var fits = (a.Interest, p.Stream) switch
            {
                ("tech", "Tech") => "You enjoy building things with technology",
                ("people", "Business") => "You enjoy creating content and working with people",
                ("numbers", "Finance") => "You enjoy working with numbers and money",
                _ => null
            };
            if (fits is not null) { score += 50; reasons.Add(fits); }

            // What they want next.
            if (a.Goal == "own-business" && p.Stream is "Business" or "Finance")
            { score += 20; reasons.Add("Useful for running your own business or freelancing"); }
            if (a.Goal is "learnership" && p.Stream is "Finance" or "Tech")
            { score += 10; reasons.Add("Many learnerships are offered in this field"); }
            if (a.Goal is "first-job" or "better-job") score += 5;

            // Not sure yet: lean towards the shortest path to a first win.
            if (a.Interest == "unsure")
            {
                score += Math.Max(0, 30 - p.TotalHours / 2);
                if (reasons.Count == 0) reasons.Add("A good first path: short steps and a quick first win");
            }

            var weeks = Math.Max(1, (int)Math.Ceiling(p.TotalHours / (double)hours));
            reasons.Add($"About {weeks} week{(weeks == 1 ? "" : "s")} at {hours} hours a week");
            return new PathRecommendation(p, score, reasons, weeks);
        })
        .OrderByDescending(r => r.Score).ThenBy(r => r.Path.TotalHours)
        .ToList();

        var tips = new List<string>();
        if (a.Connectivity is "prepaid" or "community")
            tips.Add("Start with the Indlela lessons on your roadmap: they're text-first and use very little data. Save provider videos for Wi-Fi.");
        if (a.Experience == "lots")
            tips.Add("You already have experience, so you can go straight to step 2 and come back to step 1's quiz to prove what you know.");
        if (a.Education == "school")
            tips.Add("You're still at school: study a little each week alongside your schoolwork. Your portfolio will be ready by the time you finish.");
        if (hours <= 2)
            tips.Add("Two hours a week is enough to keep moving: one short lesson on two different days works best.");

        return new SmartStartResultVm { Best = ranked[0], Others = ranked.Skip(1).ToList(), Tips = tips, HoursPerWeek = hours };
    }

    public async Task SaveProfileAsync(string userId, SmartStartInput a, string recommendedSlug, CancellationToken ct = default)
    {
        var p = await db.LearnerProfiles.FirstOrDefaultAsync(x => x.UserId == userId, ct);
        if (p is null) db.LearnerProfiles.Add(p = new LearnerProfile { UserId = userId });
        p.Interest = a.Interest; p.Education = a.Education; p.Experience = a.Experience;
        p.HoursPerWeek = Math.Clamp(a.HoursPerWeek, 1, 40); p.Connectivity = a.Connectivity; p.Goal = a.Goal;
        p.RecommendedPathSlug = recommendedSlug; p.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    // =====================================================================
    // Skills: built from what the learner actually did, with the strongest proof kept
    // =====================================================================
    public async Task<IReadOnlyList<SkillProof>> GetSkillsAsync(string userId, CancellationToken ct = default)
    {
        var found = new List<SkillProof>();

        // Indlela lessons on roadmaps: a completed in-app course = verified by quiz.
        var completed = await db.LearnerEnrollments.AsNoTracking()
            .Where(e => e.UserId == userId && e.CompletedAt != null)
            .Select(e => new { e.CourseId, e.CompletedAt, e.CompletionSource, e.Course!.Title, e.Course.Provider, e.Course.Category })
            .ToListAsync(ct);
        var completedIds = completed.Select(c => c.CourseId).ToList();

        var lessonSteps = await db.RoadmapSteps.AsNoTracking()
            .Where(s => s.CourseId != null && completedIds.Contains(s.CourseId.Value))
            .Select(s => new { s.CourseId, s.Skills, s.Title })
            .ToListAsync(ct);
        foreach (var s in lessonSteps)
        {
            var c = completed.First(x => x.CourseId == s.CourseId);
            foreach (var skill in Split(s.Skills))
                found.Add(new SkillProof(skill, ProofKind.Verified, $"Passed the Indlela quiz: {s.Title}", null, c.CompletedAt));
        }

        // Other completed courses (e.g. synced from a provider): the course's field counts as a skill.
        foreach (var c in completed.Where(c => lessonSteps.All(s => s.CourseId != c.CourseId)))
        {
            var verified = c.CompletionSource is ProgressSource.InApp or ProgressSource.ProviderSync;
            found.Add(new SkillProof(c.Category, verified ? ProofKind.Verified : ProofKind.SelfReported,
                $"Completed {c.Title} ({c.Provider})", null, c.CompletedAt));
        }

        // Provider courses and projects ticked off on roadmaps.
        var ticked = await db.LearnerStepProgress.AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => new { p.EvidenceUrl, p.CompletedAt, p.RoadmapStep!.Skills, p.RoadmapStep.Title, p.RoadmapStep.Type, p.RoadmapStep.ProviderName })
            .ToListAsync(ct);
        foreach (var t in ticked)
        {
            var kind = string.IsNullOrEmpty(t.EvidenceUrl) ? ProofKind.SelfReported
                : t.Type == RoadmapStepType.Capstone ? ProofKind.Project : ProofKind.Certificate;
            var source = t.Type == RoadmapStepType.Capstone ? $"Project: {t.Title}" : $"{t.Title} ({t.ProviderName})";
            foreach (var skill in Split(t.Skills))
                found.Add(new SkillProof(skill, kind, source, t.EvidenceUrl, t.CompletedAt));
        }

        // One row per skill, keeping the strongest proof.
        return found
            .GroupBy(s => s.Skill, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(s => s.Proof).ThenByDescending(s => s.Date).First())
            .OrderByDescending(s => s.Proof).ThenBy(s => s.Skill)
            .ToList();
    }

    public async Task<IReadOnlyList<JobMatchVm>> MatchJobsAsync(IReadOnlyList<SkillProof> skills, CancellationToken ct = default)
    {
        var have = new HashSet<string>(skills.Select(s => s.Skill), StringComparer.OrdinalIgnoreCase);
        var roles = await db.JobRoles.AsNoTracking().Include(r => r.Skills).OrderBy(r => r.SortOrder).ToListAsync(ct);

        // Where each skill is taught, so a missing skill links to the roadmap that teaches it.
        var taughtIn = (await db.RoadmapSteps.AsNoTracking()
                .Select(s => new { s.Skills, s.LearningPath!.Slug, s.LearningPath.Name }).ToListAsync(ct))
            .SelectMany(s => Split(s.Skills).Select(k => (Skill: k, s.Slug, s.Name)))
            .GroupBy(x => x.Skill, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        return roles.Select(r =>
        {
            var needed = r.Skills.Select(s => s.Skill).ToList();
            var got = needed.Where(have.Contains).ToList();
            var missing = needed.Where(n => !have.Contains(n))
                .Select(n => taughtIn.TryGetValue(n, out var t) ? (n, (string?)t.Slug, (string?)t.Name) : (n, null, null))
                .ToList();
            return new JobMatchVm
            {
                Role = r, Have = got, Missing = missing,
                Percent = needed.Count == 0 ? 0 : (int)Math.Round(100.0 * got.Count / needed.Count)
            };
        })
        .OrderByDescending(m => m.Percent).ThenBy(m => m.Role.SortOrder)
        .ToList();
    }

    // =====================================================================
    // Portfolio page
    // =====================================================================
    public async Task<PortfolioProfile> EnsurePortfolioAsync(string userId, CancellationToken ct = default)
    {
        var p = await db.PortfolioProfiles.FirstOrDefaultAsync(x => x.UserId == userId, ct);
        if (p is not null) return p;

        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == userId, ct);
        var baseSlug = Regex.Replace($"{user.FirstName}-{(user.Surname.Length > 0 ? user.Surname[..1] : "")}".ToLowerInvariant(), "[^a-z0-9-]", "");
        if (baseSlug.Trim('-').Length == 0) baseSlug = "learner";
        var slug = baseSlug;
        for (var i = 2; await db.PortfolioProfiles.AnyAsync(x => x.Slug == slug, ct); i++) slug = $"{baseSlug}-{i}";

        p = new PortfolioProfile { UserId = userId, Slug = slug };
        db.PortfolioProfiles.Add(p);
        await db.SaveChangesAsync(ct);
        return p;
    }

    public async Task<string?> UserIdForSlugAsync(string slug, CancellationToken ct = default) =>
        await db.PortfolioProfiles.AsNoTracking().Where(p => p.Slug == slug && p.IsPublic).Select(p => p.UserId).FirstOrDefaultAsync(ct);

    public async Task<PortfolioVm?> GetPortfolioAsync(string userId, bool isOwner, string baseUrl, CancellationToken ct = default)
    {
        var profile = isOwner
            ? await EnsurePortfolioAsync(userId, ct)
            : await db.PortfolioProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId && p.IsPublic, ct);
        if (profile is null) return null;

        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == userId, ct);
        var skills = await GetSkillsAsync(userId, ct);
        var matches = (await MatchJobsAsync(skills, ct)).Where(m => m.Percent >= 50).Take(3).ToList();

        var badges = await db.BadgeAwards.AsNoTracking().Where(b => b.UserId == userId)
            .OrderByDescending(b => b.DateAwarded).Select(b => new { b.Badge!.Name, b.DateAwarded }).ToListAsync(ct);
        var courses = await db.LearnerEnrollments.AsNoTracking()
            .Where(e => e.UserId == userId && e.CompletedAt != null)
            .OrderByDescending(e => e.CompletedAt)
            .Select(e => new { e.Course!.Title, e.Course.Provider, e.CompletedAt, e.CompletionSource })
            .ToListAsync(ct);

        return new PortfolioVm
        {
            Profile = profile,
            FullName = $"{user.FirstName} {user.Surname}".Trim(),
            // Contact details only when the learner chose to show them. The ID number is never shown.
            Email = isOwner || profile.ShowEmail ? user.Email : null,
            Phone = isOwner || profile.ShowPhone ? user.PhoneNumber : null,
            Skills = skills,
            Badges = badges.Select(b => (b.Name, b.DateAwarded)).ToList(),
            Courses = courses.Select(c => (c.Title, c.Provider, c.CompletedAt,
                c.CompletionSource is ProgressSource.InApp or ProgressSource.ProviderSync)).ToList(),
            TopMatches = matches,
            IsOwner = isOwner,
            ShareUrl = $"{baseUrl.TrimEnd('/')}/p/{profile.Slug}"
        };
    }

    public async Task SavePortfolioAsync(string userId, PortfolioEditInput i, CancellationToken ct = default)
    {
        var p = await EnsurePortfolioAsync(userId, ct);
        p.Headline = Clean(i.Headline); p.About = Clean(i.About); p.InformalExperience = Clean(i.InformalExperience);
        p.Education = Clean(i.Education);
        p.Location = Clean(i.Location); p.ShowEmail = i.ShowEmail; p.ShowPhone = i.ShowPhone; p.IsPublic = i.IsPublic;
        p.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    // =====================================================================
    // Forecast: predicts the finish date and spots drop-off from the learner's own pattern
    // =====================================================================
    public async Task<ForecastVm?> ForecastAsync(string userId, CancellationToken ct = default)
    {
        var slug = await roadmaps.GetActiveSlugAsync(userId, ct);
        if (slug is null) return null;
        var path = await roadmaps.GetAsync(slug, userId, ct);
        if (path is null) return null;

        var profile = await GetProfileAsync(userId, ct);
        var plannedHours = profile?.HoursPerWeek ?? 5;

        // Hours still to go: unfinished steps, minus progress already made on lessons.
        var remaining = path.Steps.Where(s => !s.IsDone)
            .Sum(s => (s.EstimatedHours ?? 2) * (1 - s.Percent / 100.0));

        // The learner's activity pattern: lesson progress + steps ticked off.
        var since = DateTime.UtcNow.AddDays(-56);
        var activity = await db.ProgressEntries.AsNoTracking()
            .Where(p => p.Enrollment!.UserId == userId && p.Timestamp >= since)
            .Select(p => p.Timestamp).ToListAsync(ct);
        activity.AddRange(await db.LearnerStepProgress.AsNoTracking()
            .Where(p => p.UserId == userId && p.CompletedAt >= since)
            .Select(p => p.CompletedAt).ToListAsync(ct));

        var sast = TimeSpan.FromHours(2);
        var days = activity.Select(t => (t + sast).Date).Distinct().ToList();
        var last = activity.Count == 0 ? (DateTime?)null : activity.Max();
        int? daysSince = last is null ? null : (int)(DateTime.UtcNow - last.Value).TotalDays;

        // Consistency over the last two weeks: about 4 study days in 14 is "on plan".
        var recentDays = days.Count(d => d >= DateTime.UtcNow.AddDays(-14).Date);
        var consistency = days.Count == 0 ? 1.0 : Math.Clamp(recentDays / 4.0, 0.25, 1.0);
        var effective = plannedHours * consistency;

        var usual = days.GroupBy(d => d.DayOfWeek).OrderByDescending(g => g.Count()).Take(2)
            .Where(g => g.Count() >= 2).Select(g => g.Key.ToString()).ToList();

        DateTime? finish = remaining <= 0 ? null : DateTime.UtcNow.Date.AddDays(Math.Ceiling(remaining / effective * 7));

        string status, headline, nudge;
        if (path.Percent >= 100 || remaining <= 0)
        {
            status = "done"; headline = "Roadmap complete";
            nudge = "Every step is done. Share your portfolio and check the jobs you match.";
        }
        else if (daysSince is null)
        {
            status = "not-started"; headline = $"At {plannedHours} hours a week, you'll finish around {finish:d MMMM}";
            nudge = $"Start step {path.NextStep?.Order ?? 1} today. Starting this week builds the habit.";
        }
        else if (daysSince >= 10)
        {
            status = "at-risk"; headline = "Your roadmap has gone quiet";
            nudge = $"It's been {daysSince} days. Long gaps make it harder to start again, so one 20-minute lesson today gets you back on track.";
        }
        else if (daysSince >= 5 || consistency < 0.5)
        {
            status = "slipping"; headline = $"At your current pace, you'll finish around {finish:d MMMM}";
            nudge = usual.Count > 0
                ? $"You usually study on {string.Join(" and ", usual)}s. Keep that rhythm to finish sooner."
                : $"Study on two fixed days a week to bring your finish date closer.";
        }
        else
        {
            status = "on-track"; headline = $"You're on track to finish around {finish:d MMMM}";
            nudge = usual.Count > 0 ? $"Nice rhythm: you usually study on {string.Join(" and ", usual)}s." : "Keep going: you're studying regularly.";
        }

        return new ForecastVm
        {
            PathName = path.Name, PathSlug = path.Slug, Percent = path.Percent,
            RemainingHours = Math.Round(remaining, 1), EffectiveHoursPerWeek = Math.Round(effective, 1),
            FinishDate = finish, DaysSinceActive = daysSince, UsualDays = usual,
            Status = status, Headline = headline, Nudge = nudge
        };
    }

    private static IEnumerable<string> Split(string? v) =>
        string.IsNullOrWhiteSpace(v) ? [] : v.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
