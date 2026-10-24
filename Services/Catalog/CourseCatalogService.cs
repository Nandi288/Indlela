using Indlela.Data;
using Indlela.Models;
using Indlela.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Indlela.Services.Catalog;

public interface ICourseCatalogService
{
    /// <summary>UC-04 + UC-05: published, recently verified courses matching the filters, one page at a time.</summary>
    Task<CourseCatalogVm> SearchAsync(CourseFilterVm filter, CancellationToken ct = default);

    Task<IReadOnlyList<CourseCardVm>> FeaturedAsync(int count, CancellationToken ct = default);

    /// <summary>Distinct providers/categories for the filter dropdowns (cached 10 minutes).</summary>
    Task<(IReadOnlyList<string> Providers, IReadOnlyList<string> Categories)> FacetsAsync(CancellationToken ct = default);

    /// <summary>Base query every public listing must use: published and verified in the last 30 days.</summary>
    IQueryable<Course> Listable();
}

public sealed class CourseCatalogService(IndlelaDbContext db, IMemoryCache cache, IConfiguration config)
    : ICourseCatalogService
{
    public const int PageSize = 20;
    private static readonly TimeSpan VerificationWindow = TimeSpan.FromDays(30);
    private readonly bool _useFullText = config.GetValue("Search:UseFullText", false);

    public IQueryable<Course> Listable()
    {
        var cutoff = DateTime.UtcNow - VerificationWindow;
        return db.Courses.AsNoTracking().Where(c => c.IsPublished && c.IsFree && c.LastVerifiedAt >= cutoff);
    }

    public async Task<CourseCatalogVm> SearchAsync(CourseFilterVm filter, CancellationToken ct = default)
    {
        var query = Listable();

        if (filter.Nqf is int nqf) query = query.Where(c => c.NqfLevel == nqf);
        if (!string.IsNullOrWhiteSpace(filter.Provider)) query = query.Where(c => c.Provider == filter.Provider);
        if (!string.IsNullOrWhiteSpace(filter.Category)) query = query.Where(c => c.Category == filter.Category);
        if (filter.Mode is DeliveryMode mode) query = query.Where(c => c.DeliveryMode == mode);
        if (filter.Accredited is bool accredited) query = query.Where(c => c.IsAccredited == accredited);

        var terms = SplitTerms(filter.Q);
        if (terms.Count > 0)
            query = _useFullText ? ApplyFullText(query, filter.Q!) : ApplyLike(query, terms);

        var total = await query.CountAsync(ct);
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        filter.Page = Math.Clamp(filter.Page, 1, totalPages);

        // Rank: title matches first (when searching), then most recently verified.
        var firstTerm = terms.FirstOrDefault();
        var ordered = firstTerm is null
            ? query.OrderByDescending(c => c.LastVerifiedAt).ThenBy(c => c.Title)
            : query.OrderByDescending(c => c.Title.Contains(firstTerm)).ThenByDescending(c => c.LastVerifiedAt);

        var courses = await ordered
            .Skip((filter.Page - 1) * PageSize)
            .Take(PageSize)
            .Select(ToCardProjection)
            .ToListAsync(ct);

        var (providers, categories) = await FacetsAsync(ct);

        return new CourseCatalogVm
        {
            Filter = filter,
            Courses = courses,
            TotalCount = total,
            PageSize = PageSize,
            Providers = providers,
            Categories = categories
        };
    }

    public async Task<IReadOnlyList<CourseCardVm>> FeaturedAsync(int count, CancellationToken ct = default) =>
        await Listable()
            .OrderByDescending(c => c.IsAccredited)
            .ThenByDescending(c => c.LastVerifiedAt)
            .Take(count)
            .Select(ToCardProjection)
            .ToListAsync(ct);

    public async Task<(IReadOnlyList<string> Providers, IReadOnlyList<string> Categories)> FacetsAsync(
        CancellationToken ct = default)
    {
        return await cache.GetOrCreateAsync("catalog:facets", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            var providers = await Listable().Select(c => c.Provider).Distinct().OrderBy(p => p).ToListAsync(ct);
            var categories = await Listable().Select(c => c.Category).Distinct().OrderBy(p => p).ToListAsync(ct);
            return ((IReadOnlyList<string>)providers, (IReadOnlyList<string>)categories);
        });
    }

    private static readonly System.Linq.Expressions.Expression<Func<Course, CourseCardVm>> ToCardProjection =
        c => new CourseCardVm(c.Id, c.Title, c.Slug, c.Provider, c.Category, c.NqfLevel, c.IsAccredited,
            c.AccreditationBody, c.DeliveryMode, c.EstimatedHours, c.IsFree);

    private static List<string> SplitTerms(string? q) =>
        string.IsNullOrWhiteSpace(q)
            ? []
            : q.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
               .Where(t => t.Length >= 2)
               .Take(6)
               .ToList();

    /// <summary>Works everywhere (LocalDB included). Every term must match title, description, provider or category.</summary>
    private static IQueryable<Course> ApplyLike(IQueryable<Course> query, IEnumerable<string> terms)
    {
        foreach (var term in terms)
        {
            var pattern = $"%{EscapeLike(term)}%";
            query = query.Where(c =>
                EF.Functions.Like(c.Title, pattern, @"\") ||
                EF.Functions.Like(c.Description!, pattern, @"\") ||
                EF.Functions.Like(c.Provider, pattern, @"\") ||
                EF.Functions.Like(c.Category, pattern, @"\"));
        }
        return query;
    }

    /// <summary>SQL Server full-text (run Data/Sql/FullTextIndex.sql first, then set Search:UseFullText=true).</summary>
    private static IQueryable<Course> ApplyFullText(IQueryable<Course> query, string q) =>
        query.Where(c =>
            EF.Functions.FreeText(c.Title, q) ||
            EF.Functions.FreeText(c.Description!, q) ||
            EF.Functions.FreeText(c.Provider, q));

    private static string EscapeLike(string term) =>
        term.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_").Replace("[", @"\[");
}
