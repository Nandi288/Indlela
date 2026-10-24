using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Indlela.Models;

/// <summary>
/// The unified learner profile (UC-01, UC-03). One profile is reused for every course handoff.
/// </summary>
public class ApplicationUser : IdentityUser
{
    [PersonalData, MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [PersonalData, MaxLength(100)]
    public string Surname { get; set; } = string.Empty;

    /// <summary>
    /// Optional. Only requested when a provider needs it. Encrypted at rest by a value converter in
    /// <see cref="Data.IndlelaDbContext"/> (ASP.NET Core Data Protection). Never shown in full after entry,
    /// never put in a URL, never printed on the portfolio.
    /// </summary>
    [ProtectedPersonalData, MaxLength(512)]
    public string? SouthAfricanIdOrPassport { get; set; }

    /// <summary>SAQA NQF level the learner is aiming for (1–10).</summary>
    [Range(1, 10)]
    public int? TargetSaqaNqfLevel { get; set; }

    [PersonalData, MaxLength(500)]
    public string? CareerGoal { get; set; }

    [PersonalData]
    public SaProvince? Province { get; set; }

    /// <summary>Learners under 18 may browse; consent wording differs (UC-01 business rule).</summary>
    public bool IsUnder18 { get; set; }

    /// <summary>POPIA consent, recorded per purpose with date/time.</summary>
    public ConsentTimestamps ConsentTimestamps { get; set; } = new();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<LearnerEnrollment> Enrollments { get; set; } = new List<LearnerEnrollment>();
    public ICollection<BadgeAward> BadgeAwards { get; set; } = new List<BadgeAward>();

    /// <summary>Last four characters only, for "ID on file: ••••1234" displays.</summary>
    public string? MaskedIdOrPassport =>
        string.IsNullOrEmpty(SouthAfricanIdOrPassport) || SouthAfricanIdOrPassport.Length < 4
            ? null
            : "••••" + SouthAfricanIdOrPassport[^4..];
}

/// <summary>
/// Owned type stored on the AspNetUsers row. A null timestamp means consent was not given
/// (or was withdrawn — withdrawing sets the value back to null and stamps the Withdrawn column).
/// </summary>
public class ConsentTimestamps
{
    public DateTime? TermsAcceptedAt { get; set; }
    public DateTime? PrivacyNoticeAcceptedAt { get; set; }

    /// <summary>Consent to share profile fields with providers during handoff (UC-13). Withdrawal disables autofill.</summary>
    public DateTime? AutofillSharingConsentAt { get; set; }
    public DateTime? AutofillSharingWithdrawnAt { get; set; }

    public DateTime? MarketingEmailConsentAt { get; set; }
    public DateTime? MarketingEmailWithdrawnAt { get; set; }

    public bool HasAutofillConsent => AutofillSharingConsentAt is not null;
    public bool HasMarketingConsent => MarketingEmailConsentAt is not null;
}
