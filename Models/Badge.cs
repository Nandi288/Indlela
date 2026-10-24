using System.ComponentModel.DataAnnotations;

namespace Indlela.Models;

/// <summary>
/// An Indlela badge and the rule that earns it (UC-31, UC-32).
/// Badges are Indlela's own recognition, not accredited qualifications.
/// </summary>
public class Badge
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    /// <summary>Small SVG (preferred) so badges stay data-light.</summary>
    [MaxLength(500)]
    public string? IconUrl { get; set; }

    /// <summary>When true, self-reported progress never earns this badge.</summary>
    public bool IsVerifiedOnly { get; set; }

    /// <summary>Human-readable criteria shown on the public verification page.</summary>
    [Required, MaxLength(500)]
    public string Criteria { get; set; } = string.Empty;

    // Machine-readable rule evaluated by the badge engine.
    public BadgeRuleType RuleType { get; set; }

    [Range(1, 100)]
    public int RequiredCount { get; set; } = 1;

    /// <summary>Optional: only completions in this course category count.</summary>
    [MaxLength(60)]
    public string? RuleCategory { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<BadgeAward> Awards { get; set; } = new List<BadgeAward>();
}

public class BadgeAward
{
    public int Id { get; set; }

    public int BadgeId { get; set; }
    public Badge? Badge { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public DateTime DateAwarded { get; set; } = DateTime.UtcNow;

    /// <summary>What proved the rule was met, e.g. "enrollment:42;source:ProviderSync".</summary>
    [Required, MaxLength(500)]
    public string EvidenceReference { get; set; } = string.Empty;

    /// <summary>Shown as a "Self-reported" label wherever the badge appears (UC-31 alt 2a).</summary>
    public bool IsSelfReportedEvidence { get; set; }

    /// <summary>Hidden badges are removed from the learner's public profile (UC-31 alt 4a).</summary>
    public bool IsHidden { get; set; }

    /// <summary>Public, unguessable id for /badges/verify/{code}.</summary>
    public Guid VerificationCode { get; set; } = Guid.NewGuid();
}
