using System.ComponentModel.DataAnnotations;

namespace Indlela.Models;

/// <summary>One row in the learner's progress ledger (UC-17, UC-30). Append-only.</summary>
public class ProgressEntry
{
    public long Id { get; set; }

    public int EnrollmentId { get; set; }
    public LearnerEnrollment? Enrollment { get; set; }

    [Required, MaxLength(200)]
    public string ModuleOrLessonName { get; set; } = string.Empty;

    /// <summary>Course-level percentage reached at this milestone.</summary>
    [Range(0, 100)]
    public int Percentage { get; set; }

    public ProgressSource ProgressSource { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
