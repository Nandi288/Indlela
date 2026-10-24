using System.Net;
using Indlela.Data;
using Microsoft.EntityFrameworkCore;

namespace Indlela.Services.Ingestion;

public sealed record LinkCheckSummary(int Checked, int Ok, int Broken, int Skipped);

public interface ILinkChecker
{
    /// <summary>
    /// Re-checks published course links (UC-07 alt 1a / UC-09 alt 3a). Working links refresh LastVerifiedAt —
    /// which keeps them inside the catalogue's 30-day window — and broken ones are flagged for re-verification.
    /// </summary>
    Task<LinkCheckSummary> CheckAsync(int maxCourses, TimeSpan olderThan, CancellationToken ct = default);
}

public sealed class LinkChecker(IndlelaDbContext db, IHttpClientFactory httpFactory, ILogger<LinkChecker> logger) : ILinkChecker
{
    /// <summary>Reserved demo domains (RFC 2606) used by the seed data: always treated as working.</summary>
    private static readonly string[] DemoHosts = ["example.com", "example.org", "example.net"];
    private static readonly TimeSpan PoliteDelay = TimeSpan.FromMilliseconds(750);

    public async Task<LinkCheckSummary> CheckAsync(int maxCourses, TimeSpan olderThan, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow - olderThan;
        var courses = await db.Courses
            .Where(c => c.IsPublished && c.LastVerifiedAt < cutoff)
            .OrderBy(c => c.LastVerifiedAt)
            .Take(maxCourses)
            .ToListAsync(ct);

        int ok = 0, broken = 0, skipped = 0;
        using var client = httpFactory.CreateClient(CrawlPolicy.ClientName);

        foreach (var course in courses)
        {
            if (course.OriginalUrl.StartsWith('/'))
            {
                // Hosted inside Indlela (Indlela Academy lessons, the simulated provider): always up.
                course.LastVerifiedAt = DateTime.UtcNow;
                ok++;
                continue;
            }
            if (!Uri.TryCreate(course.OriginalUrl, UriKind.Absolute, out var url)) { skipped++; continue; }

            if (DemoHosts.Any(h => url.Host.EndsWith(h, StringComparison.OrdinalIgnoreCase)))
            {
                course.LastVerifiedAt = DateTime.UtcNow;
                ok++;
                continue;
            }

            var status = await ProbeAsync(client, url, ct);
            if (status is >= 200 and < 400)
            {
                course.LastVerifiedAt = DateTime.UtcNow;
                course.IsBeingReverified = false;
                ok++;
            }
            else if (status is 404 or 410)
            {
                course.IsBeingReverified = true; // shown on the course page; drops out after 30 days unless fixed
                broken++;
                logger.LogWarning("Course {CourseId} link is broken ({Status}): {Url}", course.Id, status, url);
            }
            else
            {
                skipped++; // timeouts, 5xx, 403 bot-blocks: transient or inconclusive — try again next run
            }

            await Task.Delay(PoliteDelay, ct); // crawl politely (UC-08)
        }

        await db.SaveChangesAsync(ct);
        return new LinkCheckSummary(courses.Count, ok, broken, skipped);
    }

    private static async Task<int?> ProbeAsync(HttpClient client, Uri url, CancellationToken ct)
    {
        try
        {
            using var head = new HttpRequestMessage(HttpMethod.Head, url);
            using var response = await client.SendAsync(head, HttpCompletionOption.ResponseHeadersRead, ct);
            if (response.StatusCode is not (HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotImplemented or HttpStatusCode.Forbidden))
                return (int)response.StatusCode;

            // Some sites refuse HEAD; fall back to a GET that only reads the headers.
            using var get = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            return (int)get.StatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return null;
        }
    }
}

/// <summary>Runs the link check once a day (first run a minute after start-up).</summary>
public sealed class LinkCheckHostedService(IServiceScopeFactory scopes, ILogger<LinkCheckHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var summary = await scope.ServiceProvider.GetRequiredService<ILinkChecker>()
                    .CheckAsync(maxCourses: 500, olderThan: TimeSpan.FromHours(20), stoppingToken);
                logger.LogInformation("Link check: {Checked} checked, {Ok} ok, {Broken} broken, {Skipped} skipped",
                    summary.Checked, summary.Ok, summary.Broken, summary.Skipped);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Scheduled link check failed; will retry tomorrow.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
