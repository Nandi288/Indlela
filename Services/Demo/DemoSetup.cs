using System.Text.RegularExpressions;
using Indlela.Data;
using Indlela.Models;
using Microsoft.EntityFrameworkCore;

namespace Indlela.Services.Demo;

/// <summary>
/// Development-only demo wiring, run at start-up after seeding. Idempotent:
/// points the seeded external courses and handoff mappings at the simulated provider site (/demo-provider),
/// keeps demo courses verified, and makes sure Tech Pulse has demo stories linked to courses.
/// </summary>
public static partial class DemoSetup
{
    private static readonly string[] DemoHosts = ["example.com", "example.org", "example.net"];

    public static async Task ApplyAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IndlelaDbContext>();

        foreach (var mapping in await db.ProviderFormMappings.ToListAsync())
        {
            if (IsDemoUrl(mapping.RegistrationUrl))
                mapping.RegistrationUrl = $"/demo-provider/signup?provider={Slug(mapping.ProviderName)}";
        }

        foreach (var course in await db.Courses.ToListAsync())
        {
            var isDemo = IsDemoUrl(course.OriginalUrl) || course.OriginalUrl.StartsWith("/demo-provider/", StringComparison.Ordinal);
            if (!isDemo) continue;
            if (course.DeliveryMode == DeliveryMode.External && IsDemoUrl(course.OriginalUrl))
                course.OriginalUrl = $"/demo-provider/courses/{course.Slug}";
            course.LastVerifiedAt = DateTime.UtcNow;
            course.IsBeingReverified = false;
        }
        await db.SaveChangesAsync();

        // Tech Pulse demo stories, each aligned to a course (UC-24), re-dated so they stay "recent".
        var courseIds = await db.Courses.Where(c => c.IsPublished)
            .ToDictionaryAsync(c => c.Slug, c => (int?)c.Id);
        var stories = new[]
        {
            ("Demo: Banks report rise in phishing attacks targeting mobile users", "Cybersecurity", "https://example.org/news/phishing-rise", "intro-cybersecurity", 3),
            ("Demo: Cloud skills top the list of in-demand IT roles", "Cloud", "https://example.org/news/cloud-skills-demand", "cloud-fundamentals", 9),
            ("Demo: SA employers want junior developers who can code in C#", "Jobs & Skills", "https://example.org/news/junior-dev-demand", "intro-programming-csharp", 5),
            ("Demo: Small businesses turn to spreadsheets and data to survive load-shedding costs", "AI & Data", "https://example.org/news/data-skills-smme", "data-analysis-spreadsheets", 14)
        };
        var existing = await db.TechPulseArticles.ToDictionaryAsync(a => a.SourceUrl);
        foreach (var (title, category, url, slug, hoursAgo) in stories)
        {
            if (!existing.TryGetValue(url, out var article))
            {
                article = new TechPulseArticle { SourceUrl = url };
                db.TechPulseArticles.Add(article);
            }
            article.Title = title;
            article.Summary = "Demo headline for the Indlela walkthrough. With a News API key, live SA tech news appears alongside it.";
            article.SourceName = "Demo News";
            article.Category = category;
            article.PublishedAt = DateTime.UtcNow.AddHours(-hoursAgo);
            article.CachedAt = DateTime.UtcNow;
            article.AlignedCourseId = courseIds.GetValueOrDefault(slug);
        }
        await db.SaveChangesAsync();
    }

    public static string Slug(string text) => SlugRegex().Replace(text.ToLowerInvariant(), "-").Trim('-');

    private static bool IsDemoUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) && DemoHosts.Any(h => u.Host.EndsWith(h, StringComparison.OrdinalIgnoreCase));

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex SlugRegex();
}
