using System.ComponentModel.DataAnnotations;

namespace Indlela.Models;

/// <summary>
/// How a provider's registration form maps to profile fields (UC-12). Versioned so a broken change can be rolled back.
/// Only one active version per provider.
/// </summary>
public class ProviderFormMapping
{
    public int Id { get; set; }

    /// <summary>Matches <see cref="Course.Provider"/>.</summary>
    [Required, MaxLength(150)]
    public string ProviderName { get; set; } = string.Empty;

    public HandoffStrategy Strategy { get; set; } = HandoffStrategy.Manual;

    /// <summary>Registration page, e.g. https://provider.example/register.</summary>
    [Required, MaxLength(1000)]
    public string RegistrationUrl { get; set; } = string.Empty;

    /// <summary>
    /// JSON object mapping profile field → query parameter name (DeepLink) or CSS selector (Extension),
    /// e.g. {"FirstName":"given_name","Email":"email"}. Password and ID fields are never mapped for deep links.
    /// </summary>
    [Required]
    public string FieldMapJson { get; set; } = "{}";

    /// <summary>Provider permits a utm_source tracking parameter (UC-14 step 2).</summary>
    public bool AllowsTrackingParameter { get; set; }

    public int Version { get; set; } = 1;
    public bool IsActive { get; set; } = true;

    /// <summary>Flagged by learners ("Fields not filled?") for admin review.</summary>
    public bool NeedsReview { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Every handoff click — the denominator of the Outbound Enrollment Conversion KPI (UC-13).</summary>
public class HandoffEvent
{
    public long Id { get; set; }

    public int EnrollmentId { get; set; }
    public LearnerEnrollment? Enrollment { get; set; }

    public HandoffStrategy Strategy { get; set; }

    public int? MappingVersion { get; set; }

    /// <summary>Profile field names shared (never values), for the audit trail.</summary>
    [MaxLength(500)]
    public string SharedFields { get; set; } = string.Empty;

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Search analytics for the Time-to-Discovery KPI (UC-05 step 4). No personal data.</summary>
public class SearchEvent
{
    public long Id { get; set; }

    [MaxLength(200)]
    public string? Query { get; set; }

    [MaxLength(500)]
    public string? Filters { get; set; }

    public int ResultCount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int? ClickedCourseId { get; set; }
    public DateTime? ClickedAt { get; set; }
}
