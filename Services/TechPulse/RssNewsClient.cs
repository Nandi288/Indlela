using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace Indlela.Services.TechPulse;

/// <summary>One RSS or Atom feed Tech Pulse reads.</summary>
public sealed class RssFeed
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

/// <summary>Feeds come from "News:Feeds" in appsettings.json; these South African tech sites are the default.</summary>
public sealed class RssOptions
{
    public const string Section = "News";

    public List<RssFeed> Feeds { get; set; } = [];
    public int ItemsPerFeed { get; set; } = 15;

    public static readonly RssFeed[] Defaults =
    [
        new() { Name = "MyBroadband", Url = "https://mybroadband.co.za/news/feed" },
        new() { Name = "TechCentral", Url = "https://techcentral.co.za/feed/" },
        new() { Name = "ITWeb", Url = "https://www.itweb.co.za/rss" },
        new() { Name = "Stuff", Url = "https://stuff.co.za/feed/" }
    ];

    public IReadOnlyList<RssFeed> FeedsOrDefaults => Feeds.Count > 0 ? Feeds : Defaults;
}

/// <summary>
/// Free, keyless headlines from public RSS / Atom feeds. We store only the headline, a short summary
/// and the link — readers always go to the original site to read the article.
/// One broken feed never stops the others.
/// </summary>
public sealed class RssNewsClient(HttpClient http, Microsoft.Extensions.Options.IOptions<RssOptions> options,
    ILogger<RssNewsClient> logger) : INewsClient
{
    private const long MaxFeedBytes = 3 * 1024 * 1024; // ignore anything unreasonably large
    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";

    public async Task<IReadOnlyList<NewsItem>> FetchLatestAsync(CancellationToken ct = default)
    {
        var o = options.Value;
        var tasks = o.FeedsOrDefaults.Select(f => FetchFeedAsync(f, o.ItemsPerFeed, ct));
        var results = await Task.WhenAll(tasks);
        return results.SelectMany(r => r).ToList();
    }

    private async Task<IReadOnlyList<NewsItem>> FetchFeedAsync(RssFeed feed, int max, CancellationToken ct)
    {
        try
        {
            using var response = await http.GetAsync(feed.Url, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("RSS feed {Feed} returned {Status}", feed.Name, (int)response.StatusCode);
                return [];
            }
            if (response.Content.Headers.ContentLength > MaxFeedBytes)
            {
                logger.LogWarning("RSS feed {Feed} is too large; skipped", feed.Name);
                return [];
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            // No DTDs or external entities: feeds are untrusted input.
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null, Async = true };
            using var reader = XmlReader.Create(stream, settings);
            var doc = await XDocument.LoadAsync(reader, LoadOptions.None, ct);

            var items = Parse(doc, feed.Name).Take(max).ToList();
            logger.LogInformation("RSS feed {Feed}: {Count} headlines", feed.Name, items.Count);
            return items;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or XmlException)
        {
            logger.LogWarning(ex, "RSS feed {Feed} could not be read", feed.Name);
            return [];
        }
    }

    internal static IEnumerable<NewsItem> Parse(XDocument doc, string sourceName)
    {
        // RSS 2.0: <rss><channel><item>
        foreach (var item in doc.Descendants("item"))
        {
            var title = NewsText.Clean((string?)item.Element("title"), 300);
            var link = ((string?)item.Element("link"))?.Trim();
            if (string.IsNullOrWhiteSpace(title) || !IsWebLink(link)) continue;
            yield return new NewsItem(title, NewsText.Clean((string?)item.Element("description"), 600),
                link!, sourceName, ParseDate((string?)item.Element("pubDate")));
        }

        // Atom: <feed><entry>
        foreach (var entry in doc.Descendants(Atom + "entry"))
        {
            var title = NewsText.Clean((string?)entry.Element(Atom + "title"), 300);
            var link = entry.Elements(Atom + "link")
                .FirstOrDefault(l => (string?)l.Attribute("rel") is null or "alternate")?.Attribute("href")?.Value;
            if (string.IsNullOrWhiteSpace(title) || !IsWebLink(link)) continue;
            var summary = (string?)entry.Element(Atom + "summary") ?? (string?)entry.Element(Atom + "content");
            yield return new NewsItem(title, NewsText.Clean(summary, 600), link!, sourceName,
                ParseDate((string?)entry.Element(Atom + "published") ?? (string?)entry.Element(Atom + "updated")));
        }
    }

    private static bool IsWebLink(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp);

    internal static DateTime ParseDate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return DateTime.UtcNow;
        text = text.Trim();
        // RFC 822 ("Sat, 24 Oct 2026 14:05:00 +0200") — zone names and "+0200" offsets need tidying for .NET.
        text = text.Replace(" GMT", " +00:00").Replace(" UTC", " +00:00").Replace(" SAST", " +02:00");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"([+-]\d{2})(\d{2})$", "$1:$2");
        string[] formats = ["ddd, d MMM yyyy HH:mm:ss zzz", "ddd, d MMM yyyy HH:mm zzz", "d MMM yyyy HH:mm:ss zzz"];
        var parsed = DateTimeOffset.TryParseExact(text, formats, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var d)
                     || DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out d);
        if (!parsed) return DateTime.UtcNow;
        var utc = d.UtcDateTime;
        return utc > DateTime.UtcNow ? DateTime.UtcNow : utc; // never show a story "from the future"
    }
}
