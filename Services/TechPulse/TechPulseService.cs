using Indlela.Data;
using Indlela.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Indlela.Services.TechPulse;

public sealed record TechPulseSnapshot(IReadOnlyList<TechPulseArticle> Articles, DateTime? LastUpdatedUtc);

public interface ITechPulseService
{
    /// <summary>UC-23: cached headlines (6-hour expiry), newest first, with skill-alignment courses loaded.</summary>
    Task<TechPulseSnapshot> GetAsync(CancellationToken ct = default);

    /// <summary>UC-22 + UC-24: fetch, de-duplicate, categorise, align to courses, store, reset cache.</summary>
    Task<int> RefreshAsync(CancellationToken ct = default);
}

public sealed class TechPulseService(
    IndlelaDbContext db,
    INewsClient news,
    IMemoryCache cache,
    ILogger<TechPulseService> logger) : ITechPulseService
{
    public const string CacheKey = "techpulse:snapshot:v1";
    public static readonly TimeSpan CacheDuration = TimeSpan.FromHours(6);
    private static readonly TimeSpan Retention = TimeSpan.FromDays(30);
    private const int MaxArticles = 60;

    public async Task<TechPulseSnapshot> GetAsync(CancellationToken ct = default)
    {
        if (cache.TryGetValue(CacheKey, out TechPulseSnapshot? cached) && cached is not null)
            return cached;

        // Cache miss is also a refresh trigger (UC-22) — but only if the stored set is stale, and never for long.
        var newest = await db.TechPulseArticles.MaxAsync(a => (DateTime?)a.CachedAt, ct);
        if (newest is null || DateTime.UtcNow - newest > CacheDuration)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(8));
            try { await RefreshAsync(timeout.Token); }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                logger.LogWarning("Tech Pulse refresh timed out on cache miss; serving stored articles.");
            }
        }

        return await LoadAndCacheAsync(ct);
    }

    public async Task<int> RefreshAsync(CancellationToken ct = default)
    {
        var items = await news.FetchLatestAsync(ct);
        var added = 0;

        if (items.Count > 0)
        {
            var urls = items.Select(i => i.Url).Distinct().ToList();
            var known = await db.TechPulseArticles
                .Where(a => urls.Contains(a.SourceUrl))
                .Select(a => a.SourceUrl)
                .ToListAsync(ct);
            var seen = new HashSet<string>(known, StringComparer.OrdinalIgnoreCase);

            // Newest published course per category, for "Learn this skill" chips (UC-24).
            var published = await db.Courses
                .Where(c => c.IsPublished)
                .Select(c => new { c.Id, c.Category, c.LastVerifiedAt })
                .ToListAsync(ct);
            var courseByCategory = published
                .GroupBy(c => c.Category, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.MaxBy(c => c.LastVerifiedAt)!.Id, StringComparer.OrdinalIgnoreCase);

            var now = DateTime.UtcNow;
            foreach (var item in items)
            {
                if (item.Url.Length > 850 || !seen.Add(item.Url)) continue;

                var category = TechPulseCategoriser.Categorise(item.Title, item.Summary);
                int? alignedCourse = TechPulseCategoriser.CourseCategoryFor(category) is { } courseCategory
                                     && courseByCategory.TryGetValue(courseCategory, out var id)
                    ? id
                    : null;

                db.TechPulseArticles.Add(new TechPulseArticle
                {
                    Title = item.Title,
                    Summary = item.Summary,
                    SourceUrl = item.Url,
                    SourceName = item.SourceName,
                    Category = category,
                    PublishedAt = item.PublishedAtUtc,
                    CachedAt = now,
                    AlignedCourseId = alignedCourse
                });
                added++;
            }
        }

        // Keep the table small: only recent headlines matter.
        var cutoff = DateTime.UtcNow - Retention;
        await db.TechPulseArticles.Where(a => a.PublishedAt < cutoff).ExecuteDeleteAsync(ct);

        await db.SaveChangesAsync(ct);
        cache.Remove(CacheKey);

        logger.LogInformation("Tech Pulse refreshed: {Added} new articles", added);
        return added;
    }

    private async Task<TechPulseSnapshot> LoadAndCacheAsync(CancellationToken ct)
    {
        var articles = await db.TechPulseArticles
            .AsNoTracking()
            .Include(a => a.AlignedCourse)
            .OrderByDescending(a => a.PublishedAt)
            .Take(MaxArticles)
            .ToListAsync(ct);

        // Never link to an unpublished course.
        foreach (var a in articles.Where(a => a.AlignedCourse is { IsPublished: false }))
        {
            a.AlignedCourse = null;
            a.AlignedCourseId = null;
        }

        var snapshot = new TechPulseSnapshot(articles, articles.Count == 0 ? null : articles.Max(a => a.CachedAt));

        // Don't pin an empty result for 6 hours — retry soon instead (UC-23 alt 1a).
        var duration = articles.Count == 0 ? TimeSpan.FromMinutes(5) : CacheDuration;
        cache.Set(CacheKey, snapshot, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = duration
        });
        return snapshot;
    }
}

/// <summary>Keyword categoriser. Good enough for the MVP; replace with embeddings for UC-24 at scale.</summary>
public static class TechPulseCategoriser
{
    private static readonly (string Category, string[] Keywords)[] Rules =
    [
        ("Cybersecurity", ["cyber", "phishing", "ransomware", "breach", "malware", "hacker", "fraud", "security"]),
        ("Cloud", ["cloud", "azure", "aws", "google cloud", "data centre", "data center", "kubernetes"]),
        ("AI & Data", ["artificial intelligence", " ai ", "ai-", "machine learning", "data science", "analytics", "llm"]),
        ("Jobs & Skills", ["jobs", "hiring", "skills", "graduate", "internship", "learnership", "employment", "youth"]),
        ("Connectivity", ["fibre", "fiber", "5g", "network", "broadband", "data prices", "mobile data"])
    ];

    public static string Categorise(string title, string? summary)
    {
        var text = $" {title} {summary} ".ToLowerInvariant();
        foreach (var (category, keywords) in Rules)
            if (keywords.Any(k => text.Contains(k, StringComparison.Ordinal)))
                return category;
        return "Tech News";
    }

    /// <summary>Which course category a news category points to, if any.</summary>
    public static string? CourseCategoryFor(string newsCategory) => newsCategory switch
    {
        "Cybersecurity" => "Cybersecurity",
        "Cloud" => "Cloud",
        "AI & Data" => "Data",
        "Connectivity" => "Networking",
        _ => null
    };
}
