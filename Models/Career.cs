using System.ComponentModel.DataAnnotations;

namespace Indlela.Models;

/// <summary>Smart Start answers: what the learner told us at sign-up. Drives the roadmap recommendation and the Forecast.</summary>
public class LearnerProfile
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    /// <summary>tech | people | numbers | unsure</summary>
    [Required, MaxLength(20)] public string Interest { get; set; } = "unsure";
    /// <summary>school | matric | college | university | working</summary>
    [Required, MaxLength(20)] public string Education { get; set; } = "matric";
    /// <summary>none | some | lots</summary>
    [Required, MaxLength(20)] public string Experience { get; set; } = "none";
    [Range(1, 40)] public int HoursPerWeek { get; set; } = 5;
    /// <summary>wifi | prepaid | community</summary>
    [Required, MaxLength(20)] public string Connectivity { get; set; } = "wifi";
    /// <summary>first-job | learnership | better-job | own-business</summary>
    [Required, MaxLength(20)] public string Goal { get; set; } = "first-job";

    [MaxLength(80)] public string? RecommendedPathSlug { get; set; }
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>The learner's Skills Portfolio settings. The skills themselves are built from their verified activity.</summary>
public class PortfolioProfile
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    /// <summary>Public link: /p/{Slug}</summary>
    [Required, MaxLength(60)] public string Slug { get; set; } = string.Empty;
    [MaxLength(120)] public string? Headline { get; set; }
    [MaxLength(800)] public string? About { get; set; }
    /// <summary>Tutoring, a family business, volunteering… real experience many young people never get to show.</summary>
    [MaxLength(800)] public string? InformalExperience { get; set; }
    /// <summary>School, matric year, college or university, one per line.</summary>
    [MaxLength(600)] public string? Education { get; set; }
    [MaxLength(80)] public string? Location { get; set; }
    public bool ShowEmail { get; set; }
    public bool ShowPhone { get; set; }
    /// <summary>Private until the learner switches it on (POPIA).</summary>
    public bool IsPublic { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>An entry-level role Indlela can match learners to, with the skills it needs.</summary>
public class JobRole
{
    public int Id { get; set; }
    [Required, MaxLength(80)] public string Slug { get; set; } = string.Empty;
    [Required, MaxLength(120)] public string Title { get; set; } = string.Empty;
    /// <summary>Tech | Business | Finance — matches LearningPath.Stream.</summary>
    [Required, MaxLength(30)] public string Field { get; set; } = "Tech";
    [Required, MaxLength(400)] public string Summary { get; set; } = string.Empty;
    /// <summary>Kinds of openings: job, learnership, internship.</summary>
    [MaxLength(120)] public string OpeningTypes { get; set; } = "Entry-level jobs, learnerships, internships";
    public int SortOrder { get; set; }
    public ICollection<JobRoleSkill> Skills { get; set; } = new List<JobRoleSkill>();
}

public class JobRoleSkill
{
    public int Id { get; set; }
    public int JobRoleId { get; set; }
    public JobRole? JobRole { get; set; }
    /// <summary>Same wording as the skills on roadmap steps, e.g. "ASP.NET Core".</summary>
    [Required, MaxLength(80)] public string Skill { get; set; } = string.Empty;
}
