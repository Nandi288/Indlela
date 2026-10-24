using System.ComponentModel.DataAnnotations;

namespace Indlela.Models;

/// <summary>
/// A learner's relationship with one course (UC-13, UC-16, UC-28).
/// In-app courses are created directly as <see cref="HandoffStatus.Enrolled"/> with <see cref="HandoffStrategy.InApp"/>.
/// </summary>
public class LearnerEnrollment
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public int CourseId { get; set; }
    public Course? Course { get; set; }

    public HandoffStatus HandoffStatus { get; set; } = HandoffStatus.HandoffStarted;

    public HandoffStrategy HandoffStrategyUsed { get; set; }

    /// <summary>When the learner clicked Enroll Now / Start learning.</summary>
    public DateTime HandoffStartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the learner confirmed registration (UC-16). Null until then.</summary>
    public DateTime? EnrolledAt { get; set; }

    public HandoffProblemReason? ProblemReason { get; set; }

    [MaxLength(500)]
    public string? ProblemDetails { get; set; }

    /// <summary>Denormalised for a single-query dashboard (UC-18). The ledger is <see cref="ProgressEntries"/>.</summary>
    [Range(0, 100)]
    public int CurrentPercentage { get; set; }

    public ProgressSource? LastProgressSource { get; set; }
    public DateTime? LastActivityAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    /// <summary>Source of the entry that reached 100% — decides whether completion counts as verified.</summary>
    public ProgressSource? CompletionSource { get; set; }

    /// <summary>Public certificate code, e.g. "K7QM-4XPA-9T". Random (not guessable), created when the certificate is first shown.</summary>
    [MaxLength(12)]
    public string? CertificateCode { get; set; }

    public ICollection<ProgressEntry> ProgressEntries { get; set; } = new List<ProgressEntry>();

    public bool IsCompleted => CompletedAt is not null;
}
