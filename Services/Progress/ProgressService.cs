using Indlela.Data;
using Indlela.Models;
using Microsoft.EntityFrameworkCore;

namespace Indlela.Services.Progress;

public enum ProgressOutcome
{
    Recorded,
    /// <summary>Same lesson already recorded at this level — ignored, not double-counted (UC-17 alt 1b).</summary>
    Duplicate,
    /// <summary>Verified progress can only move forward.</summary>
    RejectedBackwards,
    /// <summary>Self-reporting is not allowed on a course that syncs live from the provider.</summary>
    SelfReportNotAllowed,
    /// <summary>Enrollment is not in a state that accepts progress (e.g. handoff not confirmed yet).</summary>
    NotEnrolled,
    NotFound
}

public sealed record ProgressResult(
    ProgressOutcome Outcome,
    LearnerEnrollment? Enrollment = null,
    IReadOnlyList<Badge>? NewBadges = null)
{
    public bool Succeeded => Outcome == ProgressOutcome.Recorded;
}

public interface IProgressService
{
    /// <summary>
    /// UC-17: the single entry point for every progress event — in-app lessons, provider sync (UC-30) and self-reports.
    /// Pass <paramref name="userId"/> for learner-initiated events so one learner can't write to another's enrollment;
    /// system sync jobs pass null.
    /// </summary>
    Task<ProgressResult> RecordAsync(int enrollmentId, string? userId, string moduleOrLessonName, int percentage,
        ProgressSource source, CancellationToken ct = default);
}

public sealed class ProgressService(IndlelaDbContext db, IBadgeEngine badges, ILogger<ProgressService> logger)
    : IProgressService
{
    public async Task<ProgressResult> RecordAsync(int enrollmentId, string? userId, string moduleOrLessonName,
        int percentage, ProgressSource source, CancellationToken ct = default)
    {
        percentage = Math.Clamp(percentage, 0, 100);
        moduleOrLessonName = moduleOrLessonName.Trim();

        var enrollment = await db.LearnerEnrollments
            .SingleOrDefaultAsync(e => e.Id == enrollmentId && (userId == null || e.UserId == userId), ct);

        if (enrollment is null) return new(ProgressOutcome.NotFound);
        if (enrollment.HandoffStatus != HandoffStatus.Enrolled) return new(ProgressOutcome.NotEnrolled, enrollment);
        if (enrollment.IsCompleted) return new(ProgressOutcome.Duplicate, enrollment);

        var isVerifiedSource = source is ProgressSource.InApp or ProgressSource.ProviderSync;

        if (source == ProgressSource.SelfReported)
        {
            // Self-reporting is the last fallback only: once a provider syncs, its data wins.
            var hasSync = await db.ProgressEntries.AnyAsync(
                p => p.EnrollmentId == enrollmentId && p.ProgressSource == ProgressSource.ProviderSync, ct);
            if (hasSync) return new(ProgressOutcome.SelfReportNotAllowed, enrollment);
        }
        else if (percentage < enrollment.CurrentPercentage && enrollment.LastProgressSource != ProgressSource.SelfReported)
        {
            // Verified progress only moves forward.
            return new(ProgressOutcome.RejectedBackwards, enrollment);
        }

        var isDuplicate = await db.ProgressEntries.AnyAsync(p =>
            p.EnrollmentId == enrollmentId &&
            p.ModuleOrLessonName == moduleOrLessonName &&
            p.ProgressSource == source &&
            p.Percentage >= percentage, ct);
        if (isDuplicate) return new(ProgressOutcome.Duplicate, enrollment);

        var now = DateTime.UtcNow;
        db.ProgressEntries.Add(new ProgressEntry
        {
            EnrollmentId = enrollmentId,
            ModuleOrLessonName = moduleOrLessonName,
            Percentage = percentage,
            ProgressSource = source,
            Timestamp = now
        });

        // Learners may correct their own self-reported figure downwards; verified sources take the maximum.
        enrollment.CurrentPercentage = isVerifiedSource
            ? Math.Max(enrollment.CurrentPercentage, percentage)
            : percentage;
        enrollment.LastProgressSource = source;
        enrollment.LastActivityAt = now;

        if (enrollment.CurrentPercentage == 100)
        {
            enrollment.CompletedAt = now;
            enrollment.CompletionSource = source;
        }

        await db.SaveChangesAsync(ct);

        logger.LogInformation("Progress {Percentage}% ({Source}) recorded for enrollment {EnrollmentId}",
            enrollment.CurrentPercentage, source, enrollmentId);

        // ProgressChanged → badge engine (UC-31). In-process for now; swap for MediatR/an event bus later.
        var newBadges = await badges.EvaluateAsync(enrollment.UserId, ct);

        return new(ProgressOutcome.Recorded, enrollment, newBadges);
    }
}
