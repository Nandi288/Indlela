using System.ComponentModel.DataAnnotations;

namespace Indlela.ViewModels.Api;

// ============================================================================
// JSON contract for the Indlela mobile app (/api/*). Kept separate from the
// Razor view models so the website can change without breaking the app.
// Indlela.Mobile/Models/ApiModels.cs mirrors these records.
// ============================================================================

// ---------- Auth ----------
public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record RefreshRequest([Required] string RefreshToken);

public class RegisterRequest
{
    [Required, StringLength(100)] public string FirstName { get; set; } = string.Empty;
    [Required, StringLength(100)] public string Surname { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(256)] public string Email { get; set; } = string.Empty;

    [Required, RegularExpression(@"^(\+27|0)[6-8][0-9]{8}$", ErrorMessage = "Enter a South African mobile number, e.g. 0821234567 or +27821234567.")]
    public string MobileNumber { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 8)] public string Password { get; set; } = string.Empty;

    public bool IsUnder18 { get; set; }

    [Range(typeof(bool), "true", "true", ErrorMessage = "You need to accept the Terms and the POPIA privacy notice to create an account.")]
    public bool AcceptTermsAndPrivacy { get; set; }

    public bool AllowAutofillSharing { get; set; } = true;
    public bool AllowMarketingEmail { get; set; }
}

/// <summary>After registering, the learner must verify their email. In demo mode the link is returned (simulated inbox).</summary>
public record RegisterResponse(string Message, string? DemoConfirmationLink);

/// <summary>Returned with 403 when the password is right but the email isn't verified yet.</summary>
public record ApiError(string Error, string? DemoConfirmationLink = null);

public record MeDto(string Id, string FirstName, string Surname, string Email, string? ActiveRoadmapSlug);

// ---------- Roadmaps ----------
public record PathCardDto(
    string Slug, string Name, string Stream, string Summary, IReadOnlyList<string> Careers,
    int StepCount, int TotalHours, int DoneCount, int Percent, bool IsActive, bool HasStarted);

public record PathStepDto(
    int Id, int Order, string Stage, string Title, string WhatYouLearn,
    string Type,            // "IndlelaLesson" | "ExternalCourse" | "Capstone"
    string Status,          // "Done" | "InProgress" | "Next" | "ToDo"
    int Percent, int? CourseId, string? ExternalUrl, string? ProviderName, string CostNote,
    int? EstimatedHours, IReadOnlyList<string> Skills, string ProofType, string? EvidenceUrl, bool IsEnrolled);

public record PathDetailsDto(
    string Slug, string Name, string Stream, string Summary, IReadOnlyList<string> Careers,
    int Percent, int DoneCount, int TotalHours, bool IsActive, string? OtherActiveName, bool IsComplete,
    IReadOnlyList<PathStepDto> Steps);

public record MarkStepDoneRequest([StringLength(500)] string? EvidenceUrl);

// ---------- Courses ----------
public record CourseCardDto(
    int Id, string Title, string Provider, string Category, int? NqfLevel, bool IsAccredited,
    string? AccreditationBody, bool LearnHere, int? EstimatedHours, bool IsFree);

public record CoursePageDto(
    IReadOnlyList<CourseCardDto> Items, int Page, int TotalPages, int TotalCount, IReadOnlyList<string> Categories);

public record CourseDetailsDto(
    int Id, string Title, string Provider, string Category, string? Description, int? NqfLevel, bool IsAccredited,
    string? AccreditationBody, bool LearnHere, int? EstimatedHours, bool IsFree, string? LicenceType,
    string OriginalUrl, string WebUrl, int? MyPercent, bool IsEnrolled);

// ---------- Dashboard ----------
public record DashboardCourseDto(int CourseId, string Title, string Provider, int Percent, string Status, bool IsCompleted, bool IsVerified);

public record DashboardBadgeDto(string Name, string Description, DateTime DateAwarded, Guid VerificationCode, int Tone);

public record NextBadgeDto(string Name, string ProgressText, int Percent);

public record DashboardDto(
    string FirstName, int? AveragePercent, int StreakDays, IReadOnlyList<bool> ActiveDaysThisWeek,
    int ActiveHere, int ActiveOnProvider, int VerifiedCredits,
    IReadOnlyList<DashboardCourseDto> Courses, IReadOnlyList<DashboardBadgeDto> Badges, NextBadgeDto? NextBadge,
    PathCardDto? ActiveRoadmap);
