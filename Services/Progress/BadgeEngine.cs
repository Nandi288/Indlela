using Indlela.Data;
using Indlela.Models;
using Microsoft.EntityFrameworkCore;

namespace Indlela.Services.Progress;

public interface IBadgeEngine
{
    /// <summary>Checks every active rule the learner hasn't met yet and issues badges with evidence (UC-31).</summary>
    Task<IReadOnlyList<Badge>> EvaluateAsync(string userId, CancellationToken ct = default);
}

public sealed class BadgeEngine(IndlelaDbContext db, ILogger<BadgeEngine> logger) : IBadgeEngine
{
    public async Task<IReadOnlyList<Badge>> EvaluateAsync(string userId, CancellationToken ct = default)
    {
        var pending = await db.Badges
            .Where(b => b.IsActive && !b.Awards.Any(a => a.UserId == userId))
            .ToListAsync(ct);
        if (pending.Count == 0) return [];

        // One small projection is enough to evaluate every rule.
        var enrollments = await db.LearnerEnrollments
            .Where(e => e.UserId == userId && e.LastProgressSource != null)
            .Select(e => new
            {
                e.Id,
                e.Course!.Category,
                e.LastProgressSource,
                e.CompletedAt,
                e.CompletionSource
            })
            .ToListAsync(ct);

        var awarded = new List<Badge>();

        foreach (var badge in pending)
        {
            BadgeAward? award = badge.RuleType switch
            {
                BadgeRuleType.FirstProgress => EvaluateFirstProgress(),
                BadgeRuleType.CoursesCompleted => EvaluateCompletions(),
                _ => null
            };

            if (award is null) continue;
            db.BadgeAwards.Add(award);
            awarded.Add(badge);

            BadgeAward? EvaluateFirstProgress()
            {
                var verified = enrollments.FirstOrDefault(e => e.LastProgressSource != ProgressSource.SelfReported);
                var any = verified ?? enrollments.FirstOrDefault();
                if (any is null || (badge.IsVerifiedOnly && verified is null)) return null;

                return NewAward(badge, userId, $"enrollment:{any.Id};source:{any.LastProgressSource}",
                    selfReported: verified is null);
            }

            BadgeAward? EvaluateCompletions()
            {
                var completed = enrollments
                    .Where(e => e.CompletedAt != null)
                    .Where(e => badge.RuleCategory == null ||
                                string.Equals(e.Category, badge.RuleCategory, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                var verified = completed.Where(e => e.CompletionSource != ProgressSource.SelfReported).ToList();

                bool selfReported;
                IEnumerable<int> evidenceIds;
                if (verified.Count >= badge.RequiredCount)
                {
                    selfReported = false;
                    evidenceIds = verified.Take(badge.RequiredCount).Select(e => e.Id);
                }
                else if (!badge.IsVerifiedOnly && completed.Count >= badge.RequiredCount)
                {
                    selfReported = true; // issued, but labelled Self-reported (UC-31 alt 2a)
                    evidenceIds = completed.Take(badge.RequiredCount).Select(e => e.Id);
                }
                else return null;

                return NewAward(badge, userId, "completed-enrollments:" + string.Join(',', evidenceIds), selfReported);
            }
        }

        if (awarded.Count == 0) return [];

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            // Two progress events raced; the unique (UserId, BadgeId) index kept the data correct.
            logger.LogWarning(ex, "Badge award race for user {UserId}; ignoring duplicate", userId);
            foreach (var entry in db.ChangeTracker.Entries<BadgeAward>().Where(e => e.State == EntityState.Added).ToList())
                entry.State = EntityState.Detached;
            return [];
        }

        return awarded;
    }

    private static BadgeAward NewAward(Badge badge, string userId, string evidence, bool selfReported) => new()
    {
        BadgeId = badge.Id,
        UserId = userId,
        EvidenceReference = evidence.Length > 500 ? evidence[..500] : evidence,
        IsSelfReportedEvidence = selfReported,
        DateAwarded = DateTime.UtcNow
    };
}
