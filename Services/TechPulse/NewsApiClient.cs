using System.Net;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Indlela.Services.TechPulse;

public sealed record NewsItem(string Title, string? Summary, string Url, string? SourceName, DateTime PublishedAtUtc);

/// <summary>Supplies tech headlines for Tech Pulse (UC-22). Swap implementations for NewsAPI, Bing News, RSS…</summary>
public interface INewsClient
{
    Task<IReadOnlyList<NewsItem>> FetchLatestAsync(CancellationToken ct = default);
}

public sealed class NewsOptions
{
    public const string Section = "News";

    public string? ApiKey { get; set; }
    public string BaseUrl { get; set; } = "https://newsapi.org/v2/";
    public string[] Keywords { get; set; } =
        ["\"South Africa\" technology", "software jobs South Africa", "AI skills", "cybersecurity South Africa"];
    public int PageSize { get; set; } = 20;
}

/// <summary>NewsAPI.org "everything" endpoint. Returns nothing (and logs) when no key is configured or the API fails.</summary>
public sealed partial class NewsApiClient(HttpClient http, Microsoft.Extensions.Options.IOptions<NewsOptions> options,
    ILogger<NewsApiClient> logger) : INewsClient
{
    private readonly NewsOptions _options = options.Value;

    public async Task<IReadOnlyList<NewsItem>> FetchLatestAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            logger.LogWarning("News:ApiKey is not configured; Tech Pulse will serve stored articles only.");
            return [];
        }

        var items = new List<NewsItem>();
        foreach (var keyword in _options.Keywords)
        {
            var url = $"everything?q={Uri.EscapeDataString(keyword)}&language=en&sortBy=publishedAt&pageSize={_options.PageSize}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("X-Api-Key", _options.ApiKey);

            try
            {
                using var response = await http.SendAsync(request, ct);
                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning("News API returned {Status} for '{Keyword}'", (int)response.StatusCode, keyword);
                    if (response.StatusCode == HttpStatusCode.TooManyRequests) break; // quota exceeded (UC-22 alt 1a)
                    continue;
                }

                var body = await response.Content.ReadFromJsonAsync<NewsApiResponse>(cancellationToken: ct);
                foreach (var a in body?.Articles ?? [])
                {
                    if (string.IsNullOrWhiteSpace(a.Title) || string.IsNullOrWhiteSpace(a.Url) || a.Title == "[Removed]")
                        continue;
                    items.Add(new NewsItem(
                        Clean(a.Title, 300)!,
                        Clean(a.Description, 600),
                        a.Url,
                        a.Source?.Name,
                        a.PublishedAt?.UtcDateTime ?? DateTime.UtcNow));
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
            {
                logger.LogWarning(ex, "News API request failed for '{Keyword}'", keyword);
            }
        }
        return items;
    }

    /// <summary>Strips HTML and trims to the column length — we store a short summary, never the article.</summary>
    internal static string? Clean(string? text, int max)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var plain = WebUtility.HtmlDecode(HtmlTag().Replace(text, " "));
        plain = Whitespace().Replace(plain, " ").Trim();
        return plain.Length <= max ? plain : plain[..(max - 1)].TrimEnd() + "…";
    }

    [GeneratedRegex("<[^>]+>")] private static partial Regex HtmlTag();
    [GeneratedRegex(@"\s+")] private static partial Regex Whitespace();

    private sealed class NewsApiResponse
    {
        [JsonPropertyName("articles")] public List<NewsApiArticle>? Articles { get; set; }
    }

    private sealed class NewsApiArticle
    {
        [JsonPropertyName("source")] public NewsApiSource? Source { get; set; }
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("description")] public string? Description { get; set; }
        [JsonPropertyName("url")] public string? Url { get; set; }
        [JsonPropertyName("publishedAt")] public DateTimeOffset? PublishedAt { get; set; }
    }

    private sealed class NewsApiSource
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
    }
}
