namespace Indlela.ViewModels;

/// <summary>Landing page (HomeController.Index).</summary>
public class HomeLandingVm
{
    public ImpactMetricsVm Metrics { get; init; } = new();
    public IReadOnlyList<CourseCardVm> FeaturedCourses { get; init; } = [];
    public IReadOnlyList<string> Categories { get; init; } = [];
    public int PublishedCourseCount { get; init; }
    public int ProviderCount { get; init; }
}

/// <summary>
/// The blueprint's impact KPIs. Each metric carries its measured value (when there is enough data)
/// and the blueprint target, so the page never presents a projection as a result.
/// </summary>
public class ImpactMetricsVm
{
    /// <summary>Enrolled ÷ Handoff started, 0–100 (UC-16). Null when fewer than <see cref="MinimumSample"/> handoffs.</summary>
    public double? OutboundConversionPercent { get; init; }
    public int HandoffSampleSize { get; init; }

    /// <summary>Median seconds from search to first course click, last 30 days (UC-05).</summary>
    public double? MedianTimeToDiscoverySeconds { get; init; }
    public int SearchSampleSize { get; init; }

    /// <summary>Blueprint projection — not measured yet (needs real page-weight telemetry).</summary>
    public int ProjectedDataSavingPercent { get; init; } = 70;

    public const int MinimumSample = 20;
    public const int TargetConversionPercent = 80;
    public const int TargetDiscoveryMinutes = 2;
}
