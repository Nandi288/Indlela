using System.Net;
using System.Text.RegularExpressions;

namespace Indlela.Services.TechPulse;

/// <summary>One headline for Tech Pulse: the title, a short summary and a link to the original article.</summary>
public sealed record NewsItem(string Title, string? Summary, string Url, string? SourceName, DateTime PublishedAtUtc);

/// <summary>Supplies tech headlines for Tech Pulse (UC-22). Indlela uses <see cref="RssNewsClient"/>.</summary>
public interface INewsClient
{
    Task<IReadOnlyList<NewsItem>> FetchLatestAsync(CancellationToken ct = default);
}

public static partial class NewsText
{
    /// <summary>Strips HTML and trims to the column length — we store a short summary, never the article.</summary>
    public static string? Clean(string? text, int max)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var plain = WebUtility.HtmlDecode(HtmlTag().Replace(text, " "));
        plain = Whitespace().Replace(plain, " ").Trim();
        return plain.Length <= max ? plain : plain[..(max - 1)].TrimEnd() + "…";
    }

    [GeneratedRegex("<[^>]+>")] private static partial Regex HtmlTag();
    [GeneratedRegex(@"\s+")] private static partial Regex Whitespace();
}
