using System.ComponentModel.DataAnnotations;

namespace Indlela.Models;

/// <summary>A free course in the catalogue (UC-04, UC-07, UC-11).</summary>
public class Course
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    /// <summary>URL-friendly, unique. Used for /courses/{slug} links.</summary>
    [Required, MaxLength(220)]
    public string Slug { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string Provider { get; set; } = string.Empty;

    /// <summary>Link to the original course page — always visible for attribution (UC-07).</summary>
    [Required, MaxLength(1000), Url]
    public string OriginalUrl { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string? Description { get; set; }

    /// <summary>e.g. Programming, Cloud, Cybersecurity, Data.</summary>
    [Required, MaxLength(60)]
    public string Category { get; set; } = "General";

    /// <summary>Only set when stated on the source page — never guessed (UC-10).</summary>
    [Range(1, 10)]
    public int? NqfLevel { get; set; }

    public int? Credits { get; set; }

    /// <summary>e.g. MICT SETA, SAQA, a university.</summary>
    [MaxLength(150)]
    public string? AccreditationBody { get; set; }

    public bool IsAccredited { get; set; }

    public DeliveryMode DeliveryMode { get; set; } = DeliveryMode.External;

    /// <summary>Recorded exactly as stated at the source, e.g. "CC BY 4.0", "All rights reserved".</summary>
    [MaxLength(100)]
    public string? LicenceType { get; set; }

    [MaxLength(40)]
    public string Language { get; set; } = "English";

    public bool IsFree { get; set; } = true;

    public int? EstimatedHours { get; set; }

    /// <summary>Optional. Off by default on mobile; must be ≤ 30 KB (UC-04).</summary>
    [MaxLength(1000)]
    public string? ThumbnailUrl { get; set; }

    public bool IsPublished { get; set; }

    /// <summary>Only courses verified in the last 30 days are listed (UC-04).</summary>
    public DateTime LastVerifiedAt { get; set; } = DateTime.UtcNow;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Set when several learners report the link broken (UC-07 alternate flow 1a).</summary>
    public bool IsBeingReverified { get; set; }

    public ICollection<LearnerEnrollment> Enrollments { get; set; } = new List<LearnerEnrollment>();
}
