using System.ComponentModel.DataAnnotations;
using Indlela.Models;

namespace Indlela.ViewModels;

/// <summary>Everything a course card needs — projected in SQL so no entity graph is loaded (UC-04).</summary>
public record CourseCardVm(
    int Id,
    string Title,
    string Slug,
    string Provider,
    string Category,
    int? NqfLevel,
    bool IsAccredited,
    string? AccreditationBody,
    DeliveryMode DeliveryMode,
    int? EstimatedHours,
    bool IsFree)
{
    public string NqfLabel => NqfLevel is int n ? $"NQF {n}" : "Non-accredited";
    public bool LearnHere => DeliveryMode == DeliveryMode.InApp;
}

/// <summary>Filter state, bound from the query string so every search is bookmarkable (UC-05 business rule).</summary>
public class CourseFilterVm
{
    [MaxLength(100)]
    public string? Q { get; set; }

    [Range(1, 10)]
    public int? Nqf { get; set; }

    [MaxLength(150)]
    public string? Provider { get; set; }

    [MaxLength(60)]
    public string? Category { get; set; }

    public DeliveryMode? Mode { get; set; }

    /// <summary>true = accredited only, false = non-accredited only, null = both.</summary>
    public bool? Accredited { get; set; }

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    public bool HasAnyFilter =>
        !string.IsNullOrWhiteSpace(Q) || Nqf is not null || !string.IsNullOrWhiteSpace(Provider)
        || !string.IsNullOrWhiteSpace(Category) || Mode is not null || Accredited is not null;

    /// <summary>Compact, non-personal description of the filters for SearchEvent analytics.</summary>
    public string Describe() =>
        string.Join(';', new[]
        {
            Nqf is null ? null : $"nqf={Nqf}",
            string.IsNullOrWhiteSpace(Provider) ? null : $"provider={Provider}",
            string.IsNullOrWhiteSpace(Category) ? null : $"category={Category}",
            Mode is null ? null : $"mode={Mode}",
            Accredited is null ? null : $"accredited={Accredited}"
        }.Where(s => s is not null));
}

public class CourseCatalogVm
{
    public CourseFilterVm Filter { get; init; } = new();
    public IReadOnlyList<CourseCardVm> Courses { get; init; } = [];
    public int TotalCount { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    /// <summary>Carried on course links so Details can record time-to-click (Time-to-Discovery KPI).</summary>
    public long? SearchEventId { get; set; }

    public IReadOnlyList<string> Providers { get; init; } = [];
    public IReadOnlyList<string> Categories { get; init; } = [];
}

public class BadgePreviewVm
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Criteria { get; init; } = string.Empty;
    public string? IconUrl { get; init; }
    public bool IsVerifiedOnly { get; init; }
}

public class CourseDetailsVm
{
    public required Course Course { get; init; }
    public IReadOnlyList<BadgePreviewVm> EarnableBadges { get; init; } = [];

    /// <summary>The signed-in learner's enrollment on this course, if any.</summary>
    public LearnerEnrollment? MyEnrollment { get; init; }

    public bool IsSignedIn { get; init; }
    public HandoffStrategy? ProviderStrategy { get; init; }
}
