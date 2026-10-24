using System.ComponentModel.DataAnnotations;

namespace Indlela.Models;

/// <summary>
/// A Tech Pulse headline (UC-22, UC-23). Only headline, source, short summary, date and link are stored —
/// the full article stays with the publisher.
/// </summary>
public class TechPulseArticle
{
    public int Id { get; set; }

    [Required, MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(600)]
    public string? Summary { get; set; }

    [Required, MaxLength(1000)]
    public string SourceUrl { get; set; } = string.Empty;

    [MaxLength(150)]
    public string? SourceName { get; set; }

    [Required, MaxLength(60)]
    public string Category { get; set; } = "General";

    public DateTime PublishedAt { get; set; }

    public DateTime CachedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Skill alignment: the free course this story points to (UC-24).</summary>
    public int? AlignedCourseId { get; set; }
    public Course? AlignedCourse { get; set; }
}
