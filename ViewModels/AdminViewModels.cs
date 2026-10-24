using System.ComponentModel.DataAnnotations;
using Indlela.Models;

namespace Indlela.ViewModels;

/// <summary>Admin home: import form, review queue (UC-11) and flagged links.</summary>
public class AdminQueueVm
{
    public IReadOnlyList<AdminCourseRowVm> Pending { get; init; } = [];
    public IReadOnlyList<AdminCourseRowVm> Flagged { get; init; } = [];
    public int PublishedCount { get; init; }
    public string? ImportUrl { get; init; }
}

public record AdminCourseRowVm(int Id, string Title, string Provider, string OriginalUrl, string Category, DateTime CreatedAt, DateTime LastVerifiedAt);

/// <summary>UC-11 review form: every field the admin can correct before publishing.</summary>
public class CourseReviewVm
{
    public int Id { get; set; }

    [Required, StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(150)]
    public string Provider { get; set; } = string.Empty;

    [StringLength(4000)]
    public string? Description { get; set; }

    [Required, StringLength(60)]
    public string Category { get; set; } = "General";

    [Range(1, 10), Display(Name = "NQF level")]
    public int? NqfLevel { get; set; }

    [Range(1, 999)]
    public int? Credits { get; set; }

    [StringLength(150), Display(Name = "Accreditation body (e.g. MICT SETA)")]
    public string? AccreditationBody { get; set; }

    [Display(Name = "Accredited (checked against the source page)")]
    public bool IsAccredited { get; set; }

    [Display(Name = "Where learners study it")]
    public DeliveryMode DeliveryMode { get; set; } = DeliveryMode.External;

    [StringLength(100), Display(Name = "Licence (exactly as stated on the source)")]
    public string? LicenceType { get; set; }

    [Display(Name = "A written agreement with the provider is on file")]
    public bool HasWrittenAgreement { get; set; }

    [StringLength(40)]
    public string Language { get; set; } = "English";

    [Display(Name = "Free for learners")]
    public bool IsFree { get; set; } = true;

    [Range(1, 5000), Display(Name = "Estimated hours")]
    public int? EstimatedHours { get; set; }

    // Read-only context for the reviewer
    public string OriginalUrl { get; set; } = string.Empty;
    public bool IsPublished { get; set; }
    public DateTime CreatedAt { get; set; }

    public static readonly string[] Categories = ["Programming", "Cloud", "Cybersecurity", "Data", "Networking", "Design", "Business", "General"];

    /// <summary>UC-11/UC-27 rule: In-app only when the licence allows reuse or a written agreement exists.</summary>
    public bool LicenceAllowsInApp =>
        HasWrittenAgreement ||
        (LicenceType is { } l && (l.StartsWith("CC", StringComparison.OrdinalIgnoreCase) || l.Contains("Creative Commons", StringComparison.OrdinalIgnoreCase)));
}
