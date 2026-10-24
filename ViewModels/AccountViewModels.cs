using System.ComponentModel.DataAnnotations;
using Indlela.Models;

namespace Indlela.ViewModels;

/// <summary>UC-01 registration form.</summary>
public class RegisterVm
{
    [Required, StringLength(100), Display(Name = "First name")]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string Surname { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    /// <summary>SA mobile: 0XX XXX XXXX or +27 XX XXX XXXX.</summary>
    [Required, Display(Name = "Mobile number")]
    [RegularExpression(@"^(\+27|0)[6-8][0-9]{8}$", ErrorMessage = "Enter a South African mobile number, e.g. 0821234567 or +27821234567.")]
    public string MobileNumber { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 8), DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Compare(nameof(Password), ErrorMessage = "Passwords do not match."), Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Range(1, 10), Display(Name = "Target NQF level")]
    public int? TargetSaqaNqfLevel { get; set; }

    [StringLength(500), Display(Name = "Career goal")]
    public string? CareerGoal { get; set; }

    public SaProvince? Province { get; set; }

    [Display(Name = "I am under 18")]
    public bool IsUnder18 { get; set; }

    [Range(typeof(bool), "true", "true", ErrorMessage = "You need to accept the Terms and the POPIA privacy notice to create an account.")]
    public bool AcceptTermsAndPrivacy { get; set; }

    /// <summary>Separate, optional consent purpose (POPIA).</summary>
    public bool AllowAutofillSharing { get; set; } = true;

    public bool AllowMarketingEmail { get; set; }

    public string? ReturnUrl { get; set; }
}

public class LoginVm
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Keep me signed in")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}
