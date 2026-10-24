using System.Text;
using System.Text.Encodings.Web;
using Indlela.Data;
using Indlela.Models;
using Indlela.Services.Demo;
using Indlela.Services.Email;
using Indlela.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace Indlela.Controllers;

/// <summary>UC-01 (register) and UC-02 (log in / log out). Password reset is the next piece to add.</summary>
[Route("account")]
public class AccountController(
    UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn,
    IAppEmailSender email,
    IWebHostEnvironment env,
    IConfiguration config,
    ILogger<AccountController> logger) : Controller
{
    private const string GenericLoginError = "That email and password combination didn't work.";

    [HttpGet("register")]
    public IActionResult Register(string? returnUrl = null) => View(new RegisterVm { ReturnUrl = returnUrl });

    [HttpPost("register"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterVm model)
    {
        if (!ModelState.IsValid) return View(model);

        var now = DateTime.UtcNow;
        var user = new ApplicationUser
        {
            UserName = model.Email.Trim(),
            Email = model.Email.Trim(),
            PhoneNumber = NormaliseSaMobile(model.MobileNumber),
            FirstName = model.FirstName.Trim(),
            Surname = model.Surname.Trim(),
            TargetSaqaNqfLevel = model.TargetSaqaNqfLevel,
            CareerGoal = string.IsNullOrWhiteSpace(model.CareerGoal) ? null : model.CareerGoal.Trim(),
            Province = model.Province,
            IsUnder18 = model.IsUnder18,
            ConsentTimestamps = new ConsentTimestamps
            {
                // POPIA: each purpose recorded separately with its own timestamp.
                TermsAcceptedAt = now,
                PrivacyNoticeAcceptedAt = now,
                AutofillSharingConsentAt = model.AllowAutofillSharing ? now : null,
                MarketingEmailConsentAt = model.AllowMarketingEmail ? now : null
            }
        };

        var result = await users.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            // UC-01 alt 3b: never reveal that an email is already registered.
            if (result.Errors.Any(e => e.Code is nameof(IdentityErrorDescriber.DuplicateUserName)
                                           or nameof(IdentityErrorDescriber.DuplicateEmail)))
            {
                ModelState.AddModelError(string.Empty,
                    "We couldn't create an account with those details. If you already have one, log in or reset your password.");
            }
            foreach (var error in result.Errors.Where(e => !e.Code.StartsWith("Duplicate", StringComparison.Ordinal)))
                ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        await users.AddToRoleAsync(user, SeedData.LearnerRole);
        var link = await BuildConfirmationLinkAsync(user);

        await email.SendAsync(user.Email!, "Verify your Indlela account",
            $"<p>Sawubona {HtmlEncoder.Default.Encode(user.FirstName)},</p>" +
            $"<p>Tap to verify your email (link valid for 24 hours):</p>" +
            $"<p><a href=\"{HtmlEncoder.Default.Encode(link)}\">Verify my email</a></p>");

        logger.LogInformation("New learner registered: {UserId}", user.Id);

        // Demo mode: no email service is connected, so the "email" is shown on screen instead (simulated inbox).
        ViewData["DevConfirmationLink"] = DemoMode.IsOn(config, env) ? link : null;
        return View("CheckEmail", user.Email);
    }

    [HttpGet("confirm-email")]
    public async Task<IActionResult> ConfirmEmail(string userId, string code)
    {
        var user = await users.FindByIdAsync(userId);
        if (user is null) return View("ConfirmEmailFailed");

        string token;
        try { token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code)); }
        catch (FormatException) { return View("ConfirmEmailFailed"); }

        var result = await users.ConfirmEmailAsync(user, token);
        if (!result.Succeeded) return View("ConfirmEmailFailed"); // UC-01 alt 5a: expired/invalid

        await signIn.SignInAsync(user, isPersistent: false);
        TempData["Flash"] = "Your email is verified. Welcome to Indlela!";
        return RedirectToAction("Index", "Courses");
    }

    [HttpGet("login")]
    public IActionResult Login(string? returnUrl = null) => View(new LoginVm { ReturnUrl = returnUrl });

    [HttpPost("login"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginVm model)
    {
        if (!ModelState.IsValid) return View(model);

        // lockoutOnFailure: 5 failures → 15-minute lockout (configured in Program.cs).
        var result = await signIn.PasswordSignInAsync(model.Email.Trim(), model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
            return LocalRedirect(Url.IsLocalUrl(model.ReturnUrl) ? model.ReturnUrl! : Url.Action("Dashboard", "Progression")!);

        // Demo mode: an account that registered but never verified gets its simulated inbox shown again,
        // but only once the password is proven correct (IsNotAllowed is returned before the password is checked).
        if (result.IsNotAllowed && DemoMode.IsOn(config, env))
        {
            var pending = await users.FindByEmailAsync(model.Email.Trim());
            if (pending is not null && !pending.EmailConfirmed && await users.CheckPasswordAsync(pending, model.Password))
            {
                ViewData["DevConfirmationLink"] = await BuildConfirmationLinkAsync(pending);
                return View("CheckEmail", pending.Email);
            }
        }

        ModelState.AddModelError(string.Empty, result switch
        {
            { IsLockedOut: true } => "Too many attempts. Please wait 15 minutes and try again.",
            { IsNotAllowed: true } => "Please verify your email first — check your inbox for the link.",
            _ => GenericLoginError
        });
        return View(model);
    }

    [Authorize, HttpPost("logout"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();
        // TODO (UC-02 step 4): revoke this device's extension tokens once the extension API exists.
        return Redirect("/");
    }

    [HttpGet("access-denied")]
    public IActionResult AccessDenied() => View();

    private async Task<string> BuildConfirmationLinkAsync(ApplicationUser user)
    {
        var token = await users.GenerateEmailConfirmationTokenAsync(user);
        var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        return Url.Action(nameof(ConfirmEmail), "Account", new { userId = user.Id, code }, Request.Scheme)!;
    }

    /// <summary>0821234567 → +27821234567.</summary>
    internal static string NormaliseSaMobile(string input) // also used by the mobile API
    {
        var digits = new string(input.Where(c => char.IsDigit(c) || c == '+').ToArray());
        return digits.StartsWith('0') ? "+27" + digits[1..] : digits;
    }
}
