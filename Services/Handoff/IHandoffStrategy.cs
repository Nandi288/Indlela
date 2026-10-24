using Indlela.Models;
using Indlela.ViewModels;

namespace Indlela.Services.Handoff;

/// <summary>Result of building a handoff for one learner and one course.</summary>
public sealed record HandoffPlan(
    HandoffStrategy Strategy,
    string TargetUrl,
    IReadOnlyList<SharedFieldVm> CopyFields,
    IReadOnlyList<string> SharedFieldNames,
    string? ExtensionPayloadJson = null);

/// <summary>One way of pre-filling a provider's registration form (UC-13 step 4).</summary>
public interface IHandoffStrategy
{
    HandoffStrategy Kind { get; }

    /// <param name="mapping">The provider's active mapping; null when none exists (Manual only).</param>
    HandoffPlan Build(ApplicationUser learner, Course course, ProviderFormMapping? mapping, bool hasAutofillConsent);
}

/// <summary>
/// The profile fields Indlela can share, and how to read them. The ID/passport number is deliberately absent:
/// it is never placed in a URL or extension payload (UC-14 business rule) — the learner types it on the provider site.
/// </summary>
public static class ShareableProfileFields
{
    public static readonly IReadOnlyDictionary<string, (string Label, Func<ApplicationUser, string?> Read)> All =
        new Dictionary<string, (string, Func<ApplicationUser, string?>)>(StringComparer.OrdinalIgnoreCase)
        {
            ["FirstName"] = ("First name", u => u.FirstName),
            ["Surname"] = ("Surname", u => u.Surname),
            ["Email"] = ("Email", u => u.Email),
            ["MobileNumber"] = ("Mobile number", u => u.PhoneNumber),
            ["Province"] = ("Province", u => u.Province?.ToDisplayName())
        };

    /// <summary>Copy-panel rows for every shareable field the learner has filled in.</summary>
    public static IReadOnlyList<SharedFieldVm> CopyPanel(ApplicationUser learner) =>
        All.Select(kv => (kv.Value.Label, Value: kv.Value.Read(learner)))
           .Where(x => !string.IsNullOrWhiteSpace(x.Value))
           .Select(x => new SharedFieldVm(x.Label, x.Value!))
           .ToList();

    public static string ToDisplayName(this SaProvince province) => province switch
    {
        SaProvince.EasternCape => "Eastern Cape",
        SaProvince.FreeState => "Free State",
        SaProvince.KwaZuluNatal => "KwaZulu-Natal",
        SaProvince.NorthWest => "North West",
        SaProvince.NorthernCape => "Northern Cape",
        SaProvince.WesternCape => "Western Cape",
        _ => province.ToString()
    };
}
