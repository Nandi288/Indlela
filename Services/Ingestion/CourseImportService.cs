using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Indlela.Data;
using Indlela.Models;
using Microsoft.EntityFrameworkCore;

namespace Indlela.Services.Ingestion;

public sealed record ImportResult(bool Succeeded, string Message, int? CourseId = null, bool AlreadyExisted = false);

public interface ICourseImportService
{
    /// <summary>
    /// Fast-path ingestion (UC-08 → UC-10 → UC-11): checks the URL and robots.txt, fetches one course page,
    /// extracts its details and saves it as an unpublished course waiting in the admin review queue.
    /// </summary>
    Task<ImportResult> ImportFromUrlAsync(string url, CancellationToken ct = default);
}

public sealed partial class CourseImportService(
    IndlelaDbContext db,
    IHttpClientFactory httpFactory,
    CrawlPolicy policy,
    ILogger<CourseImportService> logger) : ICourseImportService
{
    private const int MaxPageBytes = 3 * 1024 * 1024;

    public async Task<ImportResult> ImportFromUrlAsync(string url, CancellationToken ct = default)
    {
        if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri))
            return new(false, "That doesn't look like a full web address. Paste the whole link, starting with https://");
        if (uri.ToString().Length > 850)
            return new(false, "That link is too long to store.");

        // Already in the catalogue or queue? (UC-10 de-duplication by URL)
        var existing = await db.Courses.Where(c => c.OriginalUrl == uri.ToString())
            .Select(c => new { c.Id, c.IsPublished }).FirstOrDefaultAsync(ct);
        if (existing is not null)
            return new(true, existing.IsPublished ? "That course is already in the catalogue." : "That course is already waiting for review.",
                existing.Id, AlreadyExisted: true);

        var safe = await policy.CheckUrlAsync(uri, ct);
        if (!safe.Allowed) return new(false, safe.Reason);

        var robots = await policy.CheckRobotsAsync(uri, ct);
        if (!robots.Allowed) return new(false, robots.Reason);

        string html;
        Uri finalUrl;
        try
        {
            using var client = httpFactory.CreateClient(CrawlPolicy.ClientName);
            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return new(false, "That page needs a login, so it can't be imported (UC-27: content behind a login stays out).");
            if (!response.IsSuccessStatusCode)
                return new(false, $"The site answered with HTTP {(int)response.StatusCode}. Check the link opens in your browser.");
            var mediaType = response.Content.Headers.ContentType?.MediaType ?? "";
            if (!mediaType.Contains("html", StringComparison.OrdinalIgnoreCase))
                return new(false, $"That link is a {mediaType} file, not a web page.");
            if (response.Content.Headers.ContentLength > MaxPageBytes)
                return new(false, "That page is too large to import.");

            html = await ReadLimitedAsync(response, ct);
            finalUrl = response.RequestMessage?.RequestUri ?? uri;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Import fetch failed for {Url}", uri);
            return new(false, "Couldn't load that page. Check your internet connection and that the link opens in a browser.");
        }

        var found = CoursePageExtractor.Extract(html, finalUrl);
        if (string.IsNullOrWhiteSpace(found.Title))
            return new(false, "We opened the page but couldn't find a course title on it. This site may need the AI parser (Phase 2).");

        var course = new Course
        {
            Title = Trim(found.Title, 200)!,
            Slug = await UniqueSlugAsync(found.Title, ct),
            Provider = Trim(found.Provider, 150) ?? ProviderFromHost(finalUrl),
            OriginalUrl = uri.ToString(),
            Description = found.Description,
            Category = GuessCategory($"{found.Title} {found.Description}"),
            NqfLevel = found.NqfLevel,
            Credits = found.Credits,
            AccreditationBody = Trim(found.AccreditationBody, 150),
            IsAccredited = found.NqfLevel is not null && found.AccreditationBody is not null,
            // Only openly licensed content can ever be In-app; the admin confirms in review (UC-11).
            DeliveryMode = DeliveryMode.External,
            LicenceType = Trim(found.LicenceType, 100),
            Language = Trim(found.Language, 40) ?? "English",
            IsFree = found.IsFree ?? true, // admin must confirm before publishing
            EstimatedHours = found.EstimatedHours,
            IsPublished = false,           // waits in the review queue — AI/extracted data is never auto-published
            LastVerifiedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        db.Courses.Add(course);
        await db.SaveChangesAsync(ct);

        var how = found.Methods.Count > 0 ? string.Join(" + ", found.Methods) : "page text";
        logger.LogInformation("Imported {Url} as pending course {CourseId} via {Method}", uri, course.Id, how);

        var note = found.IsFree == false ? " Warning: the page says this course is paid — reject it unless you confirm it's free." : "";
        return new(true, $"Imported “{course.Title}” using {how}. Found: {string.Join(", ", found.FoundFields)}. Check the details and publish.{note}", course.Id);
    }

    private static async Task<string> ReadLimitedAsync(HttpResponseMessage response, CancellationToken ct)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, ct)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > MaxPageBytes) break;
        }
        var charset = response.Content.Headers.ContentType?.CharSet;
        Encoding encoding;
        try { encoding = string.IsNullOrWhiteSpace(charset) ? Encoding.UTF8 : Encoding.GetEncoding(charset.Trim('"')); }
        catch (ArgumentException) { encoding = Encoding.UTF8; }
        return encoding.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    private async Task<string> UniqueSlugAsync(string title, CancellationToken ct)
    {
        var baseSlug = SlugRegex().Replace(title.ToLowerInvariant(), "-").Trim('-');
        if (baseSlug.Length == 0) baseSlug = "course";
        if (baseSlug.Length > 200) baseSlug = baseSlug[..200].Trim('-');

        var slug = baseSlug;
        for (var n = 2; await db.Courses.AnyAsync(c => c.Slug == slug, ct); n++)
            slug = $"{baseSlug}-{n}";
        return slug;
    }

    /// <summary>First-guess category from keywords; the admin can change it in review.</summary>
    internal static string GuessCategory(string text)
    {
        var t = $" {text.ToLowerInvariant()} ";
        if (t.Contains("cyber") || t.Contains("security") || t.Contains("phishing")) return "Cybersecurity";
        if (t.Contains("cloud") || t.Contains("azure") || t.Contains("aws")) return "Cloud";
        if (t.Contains("data ") || t.Contains("analytics") || t.Contains("excel") || t.Contains("spreadsheet") || t.Contains(" ai ") || t.Contains("machine learning")) return "Data";
        if (t.Contains("network") || t.Contains("router") || t.Contains("ccna")) return "Networking";
        if (t.Contains("program") || t.Contains("coding") || t.Contains("python") || t.Contains("c#") || t.Contains("javascript") || t.Contains("web develop") || t.Contains("software")) return "Programming";
        return "General";
    }

    private static string ProviderFromHost(Uri url)
    {
        var host = url.Host.StartsWith("www.") ? url.Host[4..] : url.Host;
        return host.Length > 150 ? host[..150] : host;
    }

    private static string? Trim(string? s, int max) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Length <= max ? s.Trim() : s[..max].Trim();

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex SlugRegex();
}
