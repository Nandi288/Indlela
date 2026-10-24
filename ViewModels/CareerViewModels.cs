using System.ComponentModel.DataAnnotations;
using Indlela.Models;

namespace Indlela.ViewModels;

// ---------- Smart Start ----------
public class SmartStartInput
{
    [Required(ErrorMessage = "Choose what you enjoy.")] public string Interest { get; set; } = string.Empty;
    [Required(ErrorMessage = "Choose where you are now.")] public string Education { get; set; } = string.Empty;
    [Required(ErrorMessage = "Choose your experience.")] public string Experience { get; set; } = string.Empty;
    [Range(1, 40, ErrorMessage = "Choose how many hours a week.")] public int HoursPerWeek { get; set; }
    [Required(ErrorMessage = "Choose how you get internet.")] public string Connectivity { get; set; } = string.Empty;
    [Required(ErrorMessage = "Choose what you want next.")] public string Goal { get; set; } = string.Empty;
}

public record PathRecommendation(PathCardVm Path, int Score, IReadOnlyList<string> Reasons, int Weeks);

public class SmartStartResultVm
{
    public required PathRecommendation Best { get; init; }
    public IReadOnlyList<PathRecommendation> Others { get; init; } = [];
    public IReadOnlyList<string> Tips { get; init; } = [];
    public int HoursPerWeek { get; init; }
}

// ---------- Skills Portfolio ----------
public enum ProofKind { SelfReported = 1, Certificate = 2, Project = 3, Verified = 4 }

public record SkillProof(string Skill, ProofKind Proof, string Source, string? Link, DateTime? Date)
{
    public string ProofLabel => Proof switch
    {
        ProofKind.Verified => "Verified by Indlela",
        ProofKind.Project => "Project",
        ProofKind.Certificate => "Certificate",
        _ => "Self-reported"
    };
}

public class PortfolioVm
{
    public required PortfolioProfile Profile { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public IReadOnlyList<SkillProof> Skills { get; init; } = [];
    public IReadOnlyList<(string Name, DateTime Date)> Badges { get; init; } = [];
    public IReadOnlyList<(string Title, string Provider, DateTime? Date, bool Verified)> Courses { get; init; } = [];
    public IReadOnlyList<JobMatchVm> TopMatches { get; init; } = [];
    public bool IsOwner { get; init; }
    public string ShareUrl { get; init; } = string.Empty;
    public int VerifiedCount => Skills.Count(s => s.Proof == ProofKind.Verified);
}

public class PortfolioEditInput
{
    [StringLength(120)] public string? Headline { get; set; }
    [StringLength(800)] public string? About { get; set; }
    [StringLength(800)] public string? InformalExperience { get; set; }
    [StringLength(80)] public string? Location { get; set; }
    public bool ShowEmail { get; set; }
    public bool ShowPhone { get; set; }
    public bool IsPublic { get; set; }
}

// ---------- Job Matching ----------
public class JobMatchVm
{
    public required JobRole Role { get; init; }
    public IReadOnlyList<string> Have { get; init; } = [];
    public IReadOnlyList<(string Skill, string? PathSlug, string? PathName)> Missing { get; init; } = [];
    public int Percent { get; init; }
    public string Band => Percent >= 80 ? "now" : Percent >= 50 ? "close" : "explore";
    public string BandLabel => Band switch { "now" => "You can do this now", "close" => "You're close", _ => "Worth exploring" };
    public string IndeedUrl => $"https://za.indeed.com/jobs?q={Uri.EscapeDataString(Role.Title)}&l=South+Africa";
    public string LinkedInUrl => $"https://www.linkedin.com/jobs/search/?keywords={Uri.EscapeDataString(Role.Title)}&location=South%20Africa";
}

public class JobsPageVm
{
    public IReadOnlyList<JobMatchVm> Matches { get; init; } = [];
    public int SkillCount { get; init; }
    public bool IsSignedIn { get; init; }
    public IEnumerable<JobMatchVm> Now => Matches.Where(m => m.Band == "now");
    public IEnumerable<JobMatchVm> Close => Matches.Where(m => m.Band == "close");
    public IEnumerable<JobMatchVm> Explore => Matches.Where(m => m.Band == "explore");
}

// ---------- Forecast ----------
public class ForecastVm
{
    public string PathName { get; init; } = string.Empty;
    public string PathSlug { get; init; } = string.Empty;
    public int Percent { get; init; }
    public double RemainingHours { get; init; }
    public double EffectiveHoursPerWeek { get; init; }
    public DateTime? FinishDate { get; init; }
    public int? DaysSinceActive { get; init; }
    public IReadOnlyList<string> UsualDays { get; init; } = [];
    /// <summary>on-track | slipping | at-risk | not-started | done</summary>
    public string Status { get; init; } = "not-started";
    public string Headline { get; init; } = string.Empty;
    public string Nudge { get; init; } = string.Empty;
}

// ---------- Badges ----------
public record BadgeRowVm(Badge Badge, DateTime? EarnedAt);
