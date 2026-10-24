using Indlela.Data;
using Indlela.Models;
using Indlela.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace Indlela.Services.Handoff;

public interface IHandoffService
{
    /// <summary>UC-13 steps 1–2: pick a strategy and list exactly what would be shared. Nothing is saved.</summary>
    Task<HandoffConfirmVm?> PreviewAsync(ApplicationUser learner, int courseId, CancellationToken ct = default);

    /// <summary>UC-13 steps 3–4: create/refresh the enrollment, log the handoff and build the target.</summary>
    Task<HandoffResultVm?> StartAsync(ApplicationUser learner, int courseId, CancellationToken ct = default);

    /// <summary>UC-28 step 1: internal enrollment for an in-app course — no external registration.</summary>
    Task<LearnerEnrollment?> StartInAppAsync(string userId, int courseId, CancellationToken ct = default);

    /// <summary>UC-16: close the loop. <paramref name="enrolled"/> false with no reason means "Not yet".</summary>
    Task<LearnerEnrollment?> ConfirmAsync(string userId, int enrollmentId, bool enrolled, HandoffProblemReason? reason,
        string? details, CancellationToken ct = default);
}

public sealed class HandoffService(
    IndlelaDbContext db,
    IEnumerable<IHandoffStrategy> strategies,
    ILogger<HandoffService> logger) : IHandoffService
{
    private readonly Dictionary<HandoffStrategy, IHandoffStrategy> _strategies = strategies.ToDictionary(s => s.Kind);

    public async Task<HandoffConfirmVm?> PreviewAsync(ApplicationUser learner, int courseId, CancellationToken ct = default)
    {
        var course = await LoadExternalCourseAsync(courseId, ct);
        if (course is null) return null;

        var mapping = await ActiveMappingAsync(course.Provider, ct);
        var plan = BuildPlan(learner, course, mapping);

        var fields = plan.SharedFieldNames
            .Select(name => ShareableProfileFields.All[name])
            .Select(f => new SharedFieldVm(f.Label, f.Read(learner) ?? string.Empty))
            .ToList();

        return new HandoffConfirmVm
        {
            Course = ToCard(course),
            Strategy = plan.Strategy,
            FieldsToShare = fields,
            HasAutofillConsent = learner.ConsentTimestamps.HasAutofillConsent,
            ProviderRequiresId = mapping?.FieldMapJson.Contains("IdOrPassport", StringComparison.OrdinalIgnoreCase) == true
        };
    }

    public async Task<HandoffResultVm?> StartAsync(ApplicationUser learner, int courseId, CancellationToken ct = default)
    {
        var course = await LoadExternalCourseAsync(courseId, ct);
        if (course is null) return null;

        var mapping = await ActiveMappingAsync(course.Provider, ct);
        var plan = BuildPlan(learner, course, mapping);

        var enrollment = await db.LearnerEnrollments
            .SingleOrDefaultAsync(e => e.UserId == learner.Id && e.CourseId == course.Id, ct);

        if (enrollment is null)
        {
            enrollment = new LearnerEnrollment { UserId = learner.Id, CourseId = course.Id };
            db.LearnerEnrollments.Add(enrollment);
        }

        // Already confirmed learners just go back to the provider; everyone else restarts the handoff.
        if (enrollment.HandoffStatus != HandoffStatus.Enrolled)
        {
            enrollment.HandoffStatus = HandoffStatus.HandoffStarted;
            enrollment.HandoffStartedAt = DateTime.UtcNow;
            enrollment.ProblemReason = null;
            enrollment.ProblemDetails = null;
        }
        enrollment.HandoffStrategyUsed = plan.Strategy;

        // Every click is logged for the Outbound Enrollment Conversion KPI. Field NAMES only, never values.
        db.HandoffEvents.Add(new HandoffEvent
        {
            Enrollment = enrollment,
            Strategy = plan.Strategy,
            MappingVersion = mapping?.Version,
            SharedFields = string.Join(',', plan.SharedFieldNames)
        });

        await db.SaveChangesAsync(ct);

        // TODO (Hangfire): BackgroundJob.Schedule<IHandoffReminderJob>(j => j.RemindAsync(enrollment.Id), TimeSpan.FromHours(24));
        logger.LogInformation("Handoff {Strategy} started for enrollment {EnrollmentId} (course {CourseId})",
            plan.Strategy, enrollment.Id, course.Id);

        return new HandoffResultVm
        {
            EnrollmentId = enrollment.Id,
            Course = ToCard(course),
            Strategy = plan.Strategy,
            TargetUrl = plan.TargetUrl,
            CopyFields = plan.CopyFields,
            ExtensionPayloadJson = plan.ExtensionPayloadJson
        };
    }

    public async Task<LearnerEnrollment?> StartInAppAsync(string userId, int courseId, CancellationToken ct = default)
    {
        var course = await db.Courses.AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == courseId && c.IsPublished && c.DeliveryMode == DeliveryMode.InApp, ct);
        if (course is null) return null;

        var enrollment = await db.LearnerEnrollments
            .SingleOrDefaultAsync(e => e.UserId == userId && e.CourseId == courseId, ct);
        if (enrollment is not null) return enrollment;

        enrollment = new LearnerEnrollment
        {
            UserId = userId,
            CourseId = courseId,
            HandoffStatus = HandoffStatus.Enrolled,
            HandoffStrategyUsed = HandoffStrategy.InApp,
            EnrolledAt = DateTime.UtcNow
        };
        db.LearnerEnrollments.Add(enrollment);
        await db.SaveChangesAsync(ct);
        return enrollment;
    }

    public async Task<LearnerEnrollment?> ConfirmAsync(string userId, int enrollmentId, bool enrolled,
        HandoffProblemReason? reason, string? details, CancellationToken ct = default)
    {
        // Scoped to the signed-in learner: one learner can never confirm another's enrollment.
        var enrollment = await db.LearnerEnrollments
            .SingleOrDefaultAsync(e => e.Id == enrollmentId && e.UserId == userId, ct);
        if (enrollment is null) return null;

        if (enrolled)
        {
            enrollment.HandoffStatus = HandoffStatus.Enrolled;
            enrollment.EnrolledAt ??= DateTime.UtcNow;
            enrollment.ProblemReason = null;
            enrollment.ProblemDetails = null;
        }
        else if (reason is not null)
        {
            enrollment.HandoffStatus = HandoffStatus.ProblemReported;
            enrollment.ProblemReason = reason;
            enrollment.ProblemDetails = string.IsNullOrWhiteSpace(details) ? null : details.Trim();

            if (reason == HandoffProblemReason.FieldsNotFilled)
                await FlagMappingAsync(enrollment.CourseId, ct);
        }
        // else: "Not yet" — status stays HandoffStarted.
        // TODO (Hangfire): BackgroundJob.Schedule<IHandoffReminderJob>(j => j.RemindAsync(enrollment.Id), TimeSpan.FromDays(3));

        await db.SaveChangesAsync(ct);
        return enrollment;
    }

    private HandoffPlan BuildPlan(ApplicationUser learner, Course course, ProviderFormMapping? mapping)
    {
        var consent = learner.ConsentTimestamps.HasAutofillConsent;

        // Preference order Deep link → Extension → Manual (UC-13 step 1). Without consent: Manual only (UC-03 rule).
        var kind = mapping is null || !consent ? HandoffStrategy.Manual : mapping.Strategy;
        if (!_strategies.TryGetValue(kind, out var strategy))
            strategy = _strategies[HandoffStrategy.Manual];

        return strategy.Build(learner, course, mapping, consent);
    }

    private Task<Course?> LoadExternalCourseAsync(int courseId, CancellationToken ct) =>
        db.Courses.AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == courseId && c.IsPublished && c.DeliveryMode == DeliveryMode.External, ct);

    private Task<ProviderFormMapping?> ActiveMappingAsync(string provider, CancellationToken ct) =>
        db.ProviderFormMappings.AsNoTracking()
            .SingleOrDefaultAsync(m => m.ProviderName == provider && m.IsActive, ct);

    private async Task FlagMappingAsync(int courseId, CancellationToken ct)
    {
        var provider = await db.Courses.Where(c => c.Id == courseId).Select(c => c.Provider).SingleAsync(ct);
        var mapping = await db.ProviderFormMappings.SingleOrDefaultAsync(m => m.ProviderName == provider && m.IsActive, ct);
        if (mapping is not null) mapping.NeedsReview = true;
    }

    private static CourseCardVm ToCard(Course c) => new(
        c.Id, c.Title, c.Slug, c.Provider, c.Category, c.NqfLevel, c.IsAccredited,
        c.AccreditationBody, c.DeliveryMode, c.EstimatedHours, c.IsFree);
}
