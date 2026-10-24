using Indlela.Models;

namespace Indlela.ViewModels;

/// <summary>
/// "Indlela Path" learner dashboard (design 2). UC-18 dashboard, UC-17 progress, UC-31 badges.
/// Built by <see cref="Services.Dashboard.DashboardService"/> in a handful of small queries.
/// </summary>
public class PathDashboardVm
{
    public string FirstName { get; init; } = string.Empty;

    // ---- Path hero ----
    /// <summary>Up to four courses drawn as beads on the path, in path order.</summary>
    public IReadOnlyList<PathStopVm> Stops { get; init; } = [];

    /// <summary>How far the gold line runs along the path, 0–100.</summary>
    public int PathFillPercent { get; init; }

    /// <summary>Label on the goal diamond, e.g. "NQF 5 goal".</summary>
    public string GoalLabel { get; init; } = "Your goal";

    /// <summary>Average progress across enrolled courses (0–100); null when there are none.</summary>
    public int? AveragePercent { get; init; }

    public int UpdatesThisMonth { get; init; }

    /// <summary>The single course the big "Continue" button opens.</summary>
    public PathCourseVm? ContinueCourse { get; init; }

    // ---- Stats ----
    public int ActiveHere { get; init; }
    public int ActiveOnProvider { get; init; }
    public int VerifiedCredits { get; init; }
    public PathCourseVm? FirstAwaiting => Courses.FirstOrDefault(c => c.IsAwaitingConfirmation);
    public int AwaitingCount => Courses.Count(c => c.IsAwaitingConfirmation);

    // ---- Week + momentum ----
    /// <summary>Mon–Sun of the current week (SAST): true where the learner logged progress.</summary>
    public IReadOnlyList<bool> ActiveDaysThisWeek { get; init; } = [];
    public int StreakDays { get; init; }
    public IReadOnlyList<MomentumWeekVm> Momentum { get; init; } = [];
    /// <summary>Last 4 weeks vs the 4 before, as a whole percentage; null when there's nothing to compare.</summary>
    public int? MomentumChangePercent { get; init; }

    // ---- Badges ----
    public IReadOnlyList<PathBadgeVm> Badges { get; init; } = [];
    public NextBadgeVm? NextBadge { get; init; }

    // ---- Course list ----
    public IReadOnlyList<PathCourseVm> Courses { get; init; } = [];

    /// <summary>Tech Pulse stories linked to a category the learner is studying (UC-24).</summary>
    public int SkillAlertCount { get; init; }

    public bool IsEmpty => Courses.Count == 0;
}

public class PathStopVm
{
    public required PathCourseVm Course { get; init; }
    public bool IsDone => Course.IsCompleted;
    /// <summary>Short label that fits under a bead.</summary>
    public string ShortTitle => Course.Title.Length <= 26 ? Course.Title : Course.Title[..24].TrimEnd() + "…";
}

public class PathCourseVm
{
    public int EnrollmentId { get; init; }
    public int CourseId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string Provider { get; init; } = string.Empty;
    public string OriginalUrl { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public DeliveryMode DeliveryMode { get; init; }
    public HandoffStatus Status { get; init; }
    public int Percentage { get; init; }
    public ProgressSource? LastSource { get; init; }
    public DateTime HandoffStartedAt { get; init; }
    public DateTime? LastActivityAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public ProgressSource? CompletionSource { get; init; }

    public bool IsCompleted => CompletedAt is not null;
    public bool IsAwaitingConfirmation => !IsCompleted && Status is HandoffStatus.HandoffStarted or HandoffStatus.ProblemReported;
    public bool IsActive => !IsCompleted && Status == HandoffStatus.Enrolled;
    public bool LearnHere => DeliveryMode == DeliveryMode.InApp;
    public bool IsVerified => (IsCompleted ? CompletionSource : LastSource) is ProgressSource.InApp or ProgressSource.ProviderSync;
    public bool CanSelfReport => !LearnHere && IsActive && LastSource != ProgressSource.ProviderSync;

    /// <summary>"Learn here · verified", "On provider · self-reported" and so on.</summary>
    public string ModeLabel
    {
        get
        {
            if (IsAwaitingConfirmation) return Status == HandoffStatus.ProblemReported ? "Sign-up problem reported" : "Did you finish signing up?";
            var where = LearnHere ? "Learn here" : "On provider";
            if (IsCompleted) return "Completed · " + (IsVerified ? "verified" : "self-reported");
            return LastSource switch
            {
                ProgressSource.ProviderSync => $"{where} · live sync",
                ProgressSource.InApp => $"{where} · verified",
                ProgressSource.SelfReported => $"{where} · self-reported",
                _ => $"{where} · not started"
            };
        }
    }
}

public record MomentumWeekVm(string Label, int Updates, int Beads, bool IsCurrent);

public class PathBadgeVm
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public DateTime DateAwarded { get; init; }
    public bool IsSelfReportedEvidence { get; init; }
    public Guid VerificationCode { get; init; }
    /// <summary>0, 1 or 2 — picks the diamond colour (gold, green, blue) so badges read as a set.</summary>
    public int Tone { get; init; }
}

public record NextBadgeVm(string Name, string ProgressText, int Percent);
