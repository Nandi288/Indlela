using System.Net;
using System.Net.Sockets;

namespace Indlela.Services.Ingestion;

/// <summary>
/// Polite-crawling rules from UC-08: only fetch public http(s) pages, never internal addresses,
/// and obey the site's robots.txt for our bot (and for "*").
/// </summary>
public sealed class CrawlPolicy(IHttpClientFactory httpFactory, ILogger<CrawlPolicy> logger)
{
    public const string ClientName = "indlela-bot";
    public const string BotName = "IndlelaBot";

    public sealed record Verdict(bool Allowed, string Reason);

    /// <summary>Rejects non-http(s) URLs and hosts that resolve to private or loopback addresses (SSRF guard).</summary>
    public async Task<Verdict> CheckUrlAsync(Uri url, CancellationToken ct)
    {
        if (url.Scheme != Uri.UriSchemeHttps && url.Scheme != Uri.UriSchemeHttp)
            return new(false, "Only http and https links can be imported.");
        if (url.IsLoopback) return new(false, "That address points at this computer.");

        try
        {
            var addresses = await Dns.GetHostAddressesAsync(url.DnsSafeHost, ct);
            if (addresses.Length == 0) return new(false, "That website's address could not be found.");
            if (addresses.Any(IsPrivate)) return new(false, "That address is on a private network, so it can't be imported.");
        }
        catch (SocketException)
        {
            return new(false, "That website's address could not be found.");
        }
        return new(true, "OK");
    }

    /// <summary>Downloads robots.txt and applies the longest matching Allow/Disallow rule for our bot, else "*".</summary>
    public async Task<Verdict> CheckRobotsAsync(Uri url, CancellationToken ct)
    {
        var robotsUrl = new Uri(url, "/robots.txt");
        string body;
        try
        {
            using var client = httpFactory.CreateClient(ClientName);
            using var response = await client.GetAsync(robotsUrl, ct);
            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
                return new(true, "No robots.txt — crawling allowed.");
            if (!response.IsSuccessStatusCode)
                return new(false, $"Couldn't read the site's robots.txt (HTTP {(int)response.StatusCode}), so we won't crawl it.");
            body = await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "robots.txt fetch failed for {Host}", url.Host);
            return new(false, "Couldn't reach the site to check its robots.txt.");
        }

        return IsAllowed(body, url.PathAndQuery)
            ? new(true, "Allowed by robots.txt.")
            : new(false, "The site's robots.txt doesn't allow crawling this page, so it's marked Blocked (UC-08).");
    }

    /// <summary>Minimal robots.txt evaluation: groups for our bot win over "*"; longest matching rule wins; Allow wins ties.</summary>
    internal static bool IsAllowed(string robotsTxt, string path)
    {
        var groups = new List<(List<string> Agents, List<(bool Allow, string Rule)> Rules)>();
        (List<string> Agents, List<(bool, string)> Rules)? current = null;
        var lastWasAgent = false;

        foreach (var raw in robotsTxt.Split('\n'))
        {
            var line = raw.Split('#')[0].Trim();
            var colon = line.IndexOf(':');
            if (colon <= 0) continue;
            var key = line[..colon].Trim().ToLowerInvariant();
            var value = line[(colon + 1)..].Trim();

            if (key == "user-agent")
            {
                if (current is null || !lastWasAgent)
                {
                    current = (new List<string>(), new List<(bool, string)>());
                    groups.Add(current.Value);
                }
                current.Value.Agents.Add(value.ToLowerInvariant());
                lastWasAgent = true;
            }
            else if (key is "allow" or "disallow" && current is not null)
            {
                lastWasAgent = false;
                if (value.Length == 0) continue; // "Disallow:" (empty) allows everything
                current.Value.Rules.Add((key == "allow", value));
            }
            else lastWasAgent = false;
        }

        var bot = BotName.ToLowerInvariant();
        var group = groups.FirstOrDefault(g => g.Agents.Any(a => a != "*" && bot.StartsWith(a)));
        if (group.Agents is null) group = groups.FirstOrDefault(g => g.Agents.Contains("*"));
        if (group.Agents is null) return true;

        (bool Allow, string Rule)? best = null;
        foreach (var rule in group.Rules)
        {
            if (!Matches(path, rule.Rule)) continue;
            if (best is null || rule.Rule.Length > best.Value.Rule.Length || (rule.Rule.Length == best.Value.Rule.Length && rule.Allow))
                best = rule;
        }
        return best?.Allow ?? true;
    }

    private static bool Matches(string path, string rule)
    {
        var anchored = rule.EndsWith('$');
        var pattern = "^" + System.Text.RegularExpressions.Regex.Escape(anchored ? rule[..^1] : rule).Replace(@"\*", ".*") + (anchored ? "$" : "");
        return System.Text.RegularExpressions.Regex.IsMatch(path, pattern);
    }

    private static bool IsPrivate(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip)) return true;
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
            return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6UniqueLocal;
        var b = ip.GetAddressBytes();
        return b[0] == 10
               || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
               || (b[0] == 192 && b[1] == 168)
               || (b[0] == 169 && b[1] == 254)
               || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)
               || b[0] == 0;
    }
}
