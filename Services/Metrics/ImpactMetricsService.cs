using Indlela.Data;
using Indlela.Models;
using Indlela.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Indlela.Services.Metrics;

public interface IImpactMetricsService
{
    Task<ImpactMetricsVm> GetAsync(CancellationToken ct = default);
}

/// <summary>
/// The blueprint's headline KPIs for the landing page (and later UC-25). Aggregates only — no personal data.
/// Cached for 10 minutes so the landing page stays a cheap request.
/// </summary>
public sealed class ImpactMetricsService(IndlelaDbContext db, IMemoryCache cache) : IImpactMetricsService
{
    public async Task<ImpactMetricsVm> GetAsync(CancellationToken ct = default) =>
        (await cache.GetOrCreateAsync("metrics:impact", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            return await ComputeAsync(ct);
        }))!;

    private async Task<ImpactMetricsVm> ComputeAsync(CancellationToken ct)
    {
        var since = DateTime.UtcNow.AddDays(-30);

        // Outbound conversion = Enrolled ÷ Handoff started (UC-16), external courses only.
        var external = db.LearnerEnrollments
            .Where(e => e.HandoffStrategyUsed != HandoffStrategy.InApp && e.HandoffStartedAt >= since);
        var started = await external.CountAsync(ct);
        var enrolled = await external.CountAsync(e => e.HandoffStatus == HandoffStatus.Enrolled, ct);

        // Time-to-discovery: search → first course click (UC-05).
        var discoverySeconds = await db.SearchEvents
            .Where(s => s.CreatedAt >= since && s.ClickedAt != null)
            .OrderByDescending(s => s.CreatedAt)
            .Take(5000)
            .Select(s => EF.Functions.DateDiffSecond(s.CreatedAt, s.ClickedAt!.Value))
            .ToListAsync(ct);

        return new ImpactMetricsVm
        {
            HandoffSampleSize = started,
            OutboundConversionPercent = started >= ImpactMetricsVm.MinimumSample
                ? Math.Round(100.0 * enrolled / started, 1)
                : null,
            SearchSampleSize = discoverySeconds.Count,
            MedianTimeToDiscoverySeconds = discoverySeconds.Count >= ImpactMetricsVm.MinimumSample
                ? Median(discoverySeconds)
                : null
        };
    }

    private static double Median(List<int> values)
    {
        values.Sort();
        var mid = values.Count / 2;
        return values.Count % 2 == 0 ? (values[mid - 1] + values[mid]) / 2.0 : values[mid];
    }
}
