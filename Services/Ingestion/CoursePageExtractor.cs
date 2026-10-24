using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;

namespace Indlela.Services.Ingestion;

/// <summary>What we could read from one course page. Every field is optional except the title.</summary>
public sealed class ExtractedCourse
{
    public string? Title { get; set; }
    public string? Provider { get; set; }
    public string? Description { get; set; }
    public string? CanonicalUrl { get; set; }
    public string? Language { get; set; }
    public int? EstimatedHours { get; set; }
    public bool? IsFree { get; set; }
    public string? LicenceType { get; set; }
    public int? NqfLevel { get; set; }
    public int? Credits { get; set; }
    public string? AccreditationBody { get; set; }

    /// <summary>"Structured data (schema.org Course)", "Page meta tags" or both — shown to the admin.</summary>
    public List<string> Methods { get; } = [];
    public List<string> FoundFields { get; } = [];
}

/// <summary>
/// Reads course details from a page's HTML without a headless browser or AI (UC-09/UC-10, fast path):
/// 1. schema.org JSON-LD (@type Course) — the most reliable source, published by many course sites;
/// 2. Open Graph / meta tags as a fallback for title, description and site name;
/// 3. a few careful text rules for SA details (NQF level, credits, SETA) — only when stated on the page.
/// Pure function of the HTML, so it is easy to unit-test.
/// </summary>
public static partial class CoursePageExtractor
{
    public static ExtractedCourse Extract(string html, Uri pageUrl)
    {
        var result = new ExtractedCourse();

        foreach (var json in JsonLdBlocks(html))
        {
            try
            {
                using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
                var course = FindCourse(doc.RootElement);
                if (course is null) continue;
                ReadSchemaCourse(course.Value, result);
                result.Methods.Add("Structured data (schema.org Course)");
                break;
            }
            catch (JsonException)
            {
                // A broken JSON-LD block on a page is common; ignore it and use the fallbacks.
            }
        }

        ReadMetaTags(html, result);
        ReadLicenceLink(html, result);
        ReadSouthAfricanDetails(html, result);

        if (result.CanonicalUrl is null && Meta(html, "og:url") is { } ogUrl && Uri.TryCreate(pageUrl, ogUrl, out var abs))
            result.CanonicalUrl = abs.ToString();

        Track(result);
        return result;
    }

    // ---------------- schema.org JSON-LD ----------------

    private static IEnumerable<string> JsonLdBlocks(string html) =>
        JsonLdRegex().Matches(html).Select(m => WebUtility.HtmlDecode(m.Groups[1].Value).Trim()).Where(s => s.Length > 0);

    private static JsonElement? FindCourse(JsonElement el)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (var item in el.EnumerateArray())
                    if (FindCourse(item) is { } found) return found;
                return null;
            case JsonValueKind.Object:
                if (IsType(el, "Course")) return el;
                if (el.TryGetProperty("@graph", out var graph)) return FindCourse(graph);
                if (el.TryGetProperty("mainEntity", out var main)) return FindCourse(main);
                return null;
            default:
                return null;
        }
    }

    private static bool IsType(JsonElement el, string type)
    {
        if (!el.TryGetProperty("@type", out var t)) return false;
        return t.ValueKind switch
        {
            JsonValueKind.String => string.Equals(t.GetString(), type, StringComparison.OrdinalIgnoreCase),
            JsonValueKind.Array => t.EnumerateArray().Any(x => string.Equals(x.GetString(), type, StringComparison.OrdinalIgnoreCase)),
            _ => false
        };
    }

    private static void ReadSchemaCourse(JsonElement c, ExtractedCourse r)
    {
        r.Title ??= Str(c, "name");
        r.Description ??= Clean(Str(c, "description"), 4000);
        r.CanonicalUrl ??= Str(c, "url");
        r.Language ??= LanguageName(Str(c, "inLanguage"));

        if (c.TryGetProperty("provider", out var provider))
            r.Provider ??= provider.ValueKind == JsonValueKind.String ? provider.GetString() : Str(provider, "name");

        if (c.TryGetProperty("isAccessibleForFree", out var free))
            r.IsFree = free.ValueKind == JsonValueKind.True
                       || (free.ValueKind == JsonValueKind.String && free.GetString()?.Equals("true", StringComparison.OrdinalIgnoreCase) == true);

        if (r.IsFree is null && c.TryGetProperty("offers", out var offers))
        {
            var offer = offers.ValueKind == JsonValueKind.Array ? offers.EnumerateArray().FirstOrDefault() : offers;
            if (offer.ValueKind == JsonValueKind.Object)
            {
                var category = Str(offer, "category");
                var price = offer.TryGetProperty("price", out var p)
                    ? (p.ValueKind == JsonValueKind.Number ? p.GetDecimal() : decimal.TryParse(p.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : (decimal?)null)
                    : null;
                if (price == 0 || string.Equals(category, "Free", StringComparison.OrdinalIgnoreCase)) r.IsFree = true;
                else if (price > 0) r.IsFree = false;
            }
        }

        var licence = Str(c, "license");
        if (licence is not null) r.LicenceType ??= LicenceFromUrl(licence) ?? licence;

        // Duration: timeRequired on the course, or courseWorkload on an instance ("PT10H", "P2W"...).
        var duration = Str(c, "timeRequired");
        if (duration is null && c.TryGetProperty("hasCourseInstance", out var inst))
        {
            var first = inst.ValueKind == JsonValueKind.Array ? inst.EnumerateArray().FirstOrDefault() : inst;
            if (first.ValueKind == JsonValueKind.Object) duration = Str(first, "courseWorkload");
        }
        r.EstimatedHours ??= HoursFromIsoDuration(duration);

        var credential = Str(c, "educationalCredentialAwarded");
        if (credential is not null && SetaRegex().Match(credential) is { Success: true } seta)
            r.AccreditationBody ??= seta.Value.Trim();
    }

    // ---------------- Meta tags ----------------

    private static void ReadMetaTags(string html, ExtractedCourse r)
    {
        var before = r.Title;
        r.Title ??= Meta(html, "og:title") ?? Meta(html, "twitter:title") ?? TitleTag(html);
        r.Description ??= Clean(Meta(html, "og:description") ?? Meta(html, "description") ?? Meta(html, "twitter:description"), 4000);
        r.Provider ??= Meta(html, "og:site_name") ?? Meta(html, "application-name");
        r.Language ??= LanguageName(HtmlLangRegex().Match(html) is { Success: true } m ? m.Groups[1].Value : null);
        if (before is null && r.Title is not null) r.Methods.Add("Page meta tags");
        else if (r.Methods.Count == 0 && (r.Description is not null || r.Provider is not null)) r.Methods.Add("Page meta tags");
    }

    private static string? Meta(string html, string name)
    {
        foreach (Match m in MetaRegex().Matches(html))
        {
            var tag = m.Value;
            var key = AttrRegex().Matches(tag)
                .Where(a => a.Groups[1].Value.Equals("property", StringComparison.OrdinalIgnoreCase) || a.Groups[1].Value.Equals("name", StringComparison.OrdinalIgnoreCase))
                .Select(a => a.Groups[3].Value).FirstOrDefault();
            if (!string.Equals(key, name, StringComparison.OrdinalIgnoreCase)) continue;
            var content = AttrRegex().Matches(tag)
                .Where(a => a.Groups[1].Value.Equals("content", StringComparison.OrdinalIgnoreCase))
                .Select(a => a.Groups[3].Value).FirstOrDefault();
            var clean = Clean(content, 1000);
            if (!string.IsNullOrWhiteSpace(clean)) return clean;
        }
        return null;
    }

    private static string? TitleTag(string html)
    {
        var m = TitleRegex().Match(html);
        if (!m.Success) return null;
        var title = Clean(m.Groups[1].Value, 300);
        // "Course name | Provider" → keep the course part
        return title?.Split([" | ", " – ", " — ", " - "], StringSplitOptions.None)[0].Trim();
    }

    // ---------------- Licence ----------------

    private static void ReadLicenceLink(string html, ExtractedCourse r)
    {
        if (r.LicenceType is not null) return;
        var rel = LicenseLinkRegex().Match(html);
        if (rel.Success) { r.LicenceType = LicenceFromUrl(rel.Groups[1].Value) ?? rel.Groups[1].Value; return; }
        var cc = CreativeCommonsUrlRegex().Match(html);
        if (cc.Success) r.LicenceType = LicenceFromUrl(cc.Value);
    }

    /// <summary>https://creativecommons.org/licenses/by-sa/4.0/ → "CC BY-SA 4.0".</summary>
    internal static string? LicenceFromUrl(string url)
    {
        var m = CcPathRegex().Match(url);
        if (m.Success) return $"CC {m.Groups[1].Value.ToUpperInvariant()} {m.Groups[2].Value}";
        if (url.Contains("publicdomain/zero", StringComparison.OrdinalIgnoreCase)) return "CC0 1.0";
        return null;
    }

    // ---------------- South African details (only when stated) ----------------

    private static void ReadSouthAfricanDetails(string html, ExtractedCourse r)
    {
        var text = VisibleText(html);
        if (r.NqfLevel is null && NqfRegex().Match(text) is { Success: true } nqf
            && int.TryParse(nqf.Groups[1].Value, out var level) && level is >= 1 and <= 10)
            r.NqfLevel = level;
        if (r.Credits is null && CreditsRegex().Match(text) is { Success: true } cr
            && int.TryParse(cr.Groups[1].Value, out var credits) && credits is > 0 and < 1000)
            r.Credits = credits;
        if (r.AccreditationBody is null && SetaRegex().Match(text) is { Success: true } seta)
            r.AccreditationBody = seta.Value.Trim();
    }

    internal static string VisibleText(string html)
    {
        var noScripts = ScriptStyleRegex().Replace(html, " ");
        var text = TagRegex().Replace(noScripts, " ");
        return SpaceRegex().Replace(WebUtility.HtmlDecode(text), " ");
    }

    // ---------------- Helpers ----------------

    internal static int? HoursFromIsoDuration(string? iso)
    {
        if (string.IsNullOrWhiteSpace(iso)) return null;
        try
        {
            var span = XmlConvert.ToTimeSpan(iso.Trim());
            var hours = (int)Math.Round(span.TotalHours);
            return hours is > 0 and < 5000 ? hours : null;
        }
        catch (FormatException) { return null; }
    }

    private static string? LanguageName(string? code) => code?.Trim().ToLowerInvariant() switch
    {
        null or "" => null,
        var c when c.StartsWith("en") => "English",
        var c when c.StartsWith("zu") => "isiZulu",
        var c when c.StartsWith("xh") => "isiXhosa",
        var c when c.StartsWith("af") => "Afrikaans",
        var c when c.StartsWith("st") => "Sesotho",
        var c when c.StartsWith("tn") => "Setswana",
        var c when c.StartsWith("fr") => "French",
        var c when c.StartsWith("pt") => "Portuguese",
        var c => c.Length <= 40 ? code : null
    };

    private static string? Str(JsonElement el, string prop) =>
        el.ValueKind == JsonValueKind.Object && el.TryGetProperty(prop, out var v)
            ? v.ValueKind switch
            {
                JsonValueKind.String => Clean(v.GetString(), 4000),
                JsonValueKind.Array => v.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()).FirstOrDefault(),
                JsonValueKind.Object => Str(v, "name"),
                _ => null
            }
            : null;

    private static string? Clean(string? s, int max)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var plain = SpaceRegex().Replace(WebUtility.HtmlDecode(TagRegex().Replace(s, " ")), " ").Trim();
        if (plain.Length == 0) return null;
        return plain.Length <= max ? plain : plain[..(max - 1)].TrimEnd() + "…";
    }

    private static void Track(ExtractedCourse r)
    {
        void Add(string name, object? value) { if (value is not null) r.FoundFields.Add(name); }
        Add("title", r.Title); Add("provider", r.Provider); Add("description", r.Description);
        Add("duration", r.EstimatedHours); Add("language", r.Language); Add("free/paid", r.IsFree);
        Add("licence", r.LicenceType); Add("NQF level", r.NqfLevel); Add("credits", r.Credits); Add("SETA", r.AccreditationBody);
    }

    [GeneratedRegex(@"<script[^>]*type\s*=\s*[""']application/ld\+json[""'][^>]*>(.*?)</script>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex JsonLdRegex();
    [GeneratedRegex(@"<meta\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex MetaRegex();
    [GeneratedRegex(@"([a-zA-Z:-]+)\s*=\s*([""'])(.*?)\2", RegexOptions.Singleline)]
    private static partial Regex AttrRegex();
    [GeneratedRegex(@"<title[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TitleRegex();
    [GeneratedRegex(@"<html[^>]*\blang\s*=\s*[""']([a-zA-Z-]+)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex HtmlLangRegex();
    [GeneratedRegex(@"<(?:link|a)\b[^>]*rel\s*=\s*[""'][^""']*\blicense\b[^""']*[""'][^>]*href\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex LicenseLinkRegex();
    [GeneratedRegex(@"https?://creativecommons\.org/(?:licenses|publicdomain)/[a-z-]+/\d\.\d", RegexOptions.IgnoreCase)]
    private static partial Regex CreativeCommonsUrlRegex();
    [GeneratedRegex(@"creativecommons\.org/licenses/([a-z-]+)/(\d\.\d)", RegexOptions.IgnoreCase)]
    private static partial Regex CcPathRegex();
    [GeneratedRegex(@"\bNQF\s*(?:level\s*)?:?\s*(\d{1,2})\b", RegexOptions.IgnoreCase)]
    private static partial Regex NqfRegex();
    [GeneratedRegex(@"\b(\d{1,3})\s*credits?\b", RegexOptions.IgnoreCase)]
    private static partial Regex CreditsRegex();
    [GeneratedRegex(@"\b(?:MICT|merSETA|MICTSETA|Services|W&RSETA|BANKSETA|INSETA|FASSET|ETDP|HWSETA|CHIETA|AgriSETA|TETA|LGSETA|SASSETA|PSETA|EWSETA|CATHSSETA|FoodBev|MQA|CETA)\s*SETA\b|\bQCTO\b", RegexOptions.IgnoreCase)]
    private static partial Regex SetaRegex();
    [GeneratedRegex(@"<(script|style|noscript)[^>]*>.*?</\1>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ScriptStyleRegex();
    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex TagRegex();
    [GeneratedRegex(@"\s+")]
    private static partial Regex SpaceRegex();
}
