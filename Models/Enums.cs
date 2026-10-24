namespace Indlela.Models;

/// <summary>Where the learner studies the course (UC-11: set by the admin from the licence).</summary>
public enum DeliveryMode
{
    /// <summary>Openly licensed or partner content imported into Indlela (UC-27, UC-28).</summary>
    InApp = 0,
    /// <summary>Studied on the provider's site via smart handoff (UC-13) and live sync (UC-30).</summary>
    External = 1
}

/// <summary>Enrollment state machine for external courses (UC-13, UC-16).</summary>
public enum HandoffStatus
{
    HandoffStarted = 0,
    Enrolled = 1,
    Abandoned = 2,
    ProblemReported = 3
}

/// <summary>How the provider form was pre-filled (UC-13 step 1: Deep link → Extension → Manual).</summary>
public enum HandoffStrategy
{
    /// <summary>No handoff needed — the course is learned inside Indlela.</summary>
    InApp = 0,
    DeepLink = 1,
    Extension = 2,
    Manual = 3
}

/// <summary>Provenance of a progress entry (UC-17). Verified and self-reported progress are never mixed up.</summary>
public enum ProgressSource
{
    InApp = 0,
    ProviderSync = 1,
    SelfReported = 2
}

/// <summary>What a badge rule listens for (UC-31, UC-32).</summary>
public enum BadgeRuleType
{
    /// <summary>First progress event on any course.</summary>
    FirstProgress = 0,
    /// <summary>Completed N courses (optionally in one category).</summary>
    CoursesCompleted = 1
}

/// <summary>Reasons a learner can give when a handoff failed (UC-16 alternate flow 2a).</summary>
public enum HandoffProblemReason
{
    FormTooLong = 0,
    FieldsNotFilled = 1,
    SiteError = 2,
    DataRanOut = 3,
    Other = 4
}

public enum SaProvince
{
    EasternCape,
    FreeState,
    Gauteng,
    KwaZuluNatal,
    Limpopo,
    Mpumalanga,
    NorthWest,
    NorthernCape,
    WesternCape
}
