using System.Text.Json;
using Indlela.Models;
using Indlela.ViewModels;
using Microsoft.AspNetCore.WebUtilities;

namespace Indlela.Services.Handoff;

/// <summary>UC-14: builds the provider's registration URL with consented fields as query parameters.</summary>
public sealed class DeepLinkStrategy : IHandoffStrategy
{
    private readonly string _utmSource;

    public DeepLinkStrategy(IConfiguration config)
    {
        _utmSource = config["Handoff:UtmSource"] ?? "indlela";
    }

    public HandoffStrategy Kind => HandoffStrategy.DeepLink;

    public HandoffPlan Build(ApplicationUser learner, Course course, ProviderFormMapping? mapping, bool hasAutofillConsent)
    {
        ArgumentNullException.ThrowIfNull(mapping);

        var fieldMap = MappingParser.Parse(mapping.FieldMapJson);
        var query = new Dictionary<string, string?>();
        var shared = new List<string>();

        if (hasAutofillConsent)
        {
            foreach (var (profileField, parameterName) in fieldMap)
            {
                // Only whitelisted, non-sensitive fields. Anything else in the mapping (ID, password) is ignored.
                if (!ShareableProfileFields.All.TryGetValue(profileField, out var field)) continue;

                var value = field.Read(learner);
                if (string.IsNullOrWhiteSpace(value)) continue;

                query[parameterName] = value;   // QueryHelpers URL-encodes
                shared.Add(profileField);
            }
        }

        if (mapping.AllowsTrackingParameter)
            query["utm_source"] = _utmSource;

        var url = QueryHelpers.AddQueryString(mapping.RegistrationUrl, query);

        // The copy panel is always prepared too, for the "Fields not filled?" fallback (UC-14 alt 3a).
        return new HandoffPlan(Kind, url, ShareableProfileFields.CopyPanel(learner), shared);
    }
}

/// <summary>
/// UC-15: prepares the payload the Manifest v3 extension would fetch. In this build the payload is shown as a
/// simulation; production issues a single-use, 5-minute HandoffToken and serves the payload from
/// /api/extension/handoff/{token} (Sprint 3).
/// </summary>
public sealed class ExtensionStrategy : IHandoffStrategy
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public HandoffStrategy Kind => HandoffStrategy.Extension;

    public HandoffPlan Build(ApplicationUser learner, Course course, ProviderFormMapping? mapping, bool hasAutofillConsent)
    {
        ArgumentNullException.ThrowIfNull(mapping);

        var selectors = MappingParser.Parse(mapping.FieldMapJson);
        var fields = new List<object>();
        var shared = new List<string>();

        if (hasAutofillConsent)
        {
            foreach (var (profileField, selector) in selectors)
            {
                if (!ShareableProfileFields.All.TryGetValue(profileField, out var field)) continue;
                var value = field.Read(learner);
                if (string.IsNullOrWhiteSpace(value)) continue;

                fields.Add(new { field = profileField, selector, value });
                shared.Add(profileField);
            }
        }

        var payload = JsonSerializer.Serialize(new
        {
            providerDomain = Uri.TryCreate(mapping.RegistrationUrl, UriKind.Absolute, out var reg) ? reg.Host : "localhost",
            mappingVersion = mapping.Version,
            expiresInSeconds = 300,
            autoSubmit = false, // the learner always clicks Submit
            fields
        }, JsonOptions);

        return new HandoffPlan(Kind, mapping.RegistrationUrl, ShareableProfileFields.CopyPanel(learner), shared, payload);
    }
}

/// <summary>UC-13 alt 1a: no mapping (or no consent) — one-tap copy buttons and the provider in a new tab.</summary>
public sealed class ManualStrategy : IHandoffStrategy
{
    public HandoffStrategy Kind => HandoffStrategy.Manual;

    public HandoffPlan Build(ApplicationUser learner, Course course, ProviderFormMapping? mapping, bool hasAutofillConsent)
    {
        var url = mapping?.RegistrationUrl ?? course.OriginalUrl;
        // Nothing is sent to the provider; the learner copies their own details.
        return new HandoffPlan(Kind, url, ShareableProfileFields.CopyPanel(learner), []);
    }
}

internal static class MappingParser
{
    public static IReadOnlyDictionary<string, string> Parse(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>(); // a broken mapping degrades to "nothing pre-filled"
        }
    }
}
