using System.ComponentModel.DataAnnotations;
using Indlela.Models;

namespace Indlela.ViewModels;

/// <summary>UC-13 step 2: the confirmation panel listing exactly what will be shared.</summary>
public class HandoffConfirmVm
{
    public required CourseCardVm Course { get; init; }
    public HandoffStrategy Strategy { get; init; }
    public IReadOnlyList<SharedFieldVm> FieldsToShare { get; init; } = [];
    public bool HasAutofillConsent { get; init; }
    public bool ProviderRequiresId { get; init; }
}

public record SharedFieldVm(string Label, string DisplayValue);

/// <summary>UC-13 step 4 result: where to send the learner, or what to copy.</summary>
public class HandoffResultVm
{
    public int EnrollmentId { get; init; }
    public required CourseCardVm Course { get; init; }
    public HandoffStrategy Strategy { get; init; }

    /// <summary>Deep link with consented fields URL-encoded (DeepLink), or the plain registration URL.</summary>
    public string TargetUrl { get; init; } = string.Empty;

    /// <summary>Manual copy panel, and the "Fields not filled?" fallback for deep links (UC-14 alt 3a).</summary>
    public IReadOnlyList<SharedFieldVm> CopyFields { get; init; } = [];

    /// <summary>Simulated payload the Manifest v3 extension would request (UC-15). Shown in the demo only.</summary>
    public string? ExtensionPayloadJson { get; init; }
}

/// <summary>UC-17 input from the learner (in-app lesson complete, or self-report on an unsynced external course).</summary>
public class UpdateProgressInput
{
    [Required]
    public int EnrollmentId { get; set; }

    [Required, StringLength(200, MinimumLength = 1)]
    public string ModuleOrLessonName { get; set; } = string.Empty;

    [Range(0, 100)]
    public int Percentage { get; set; }
}

public class TechPulseVm
{
    public IReadOnlyList<TechPulseArticle> Articles { get; init; } = [];
    public IReadOnlyList<string> Categories { get; init; } = [];
    public string? SelectedCategory { get; init; }
    public DateTime? LastUpdatedUtc { get; init; }
}
