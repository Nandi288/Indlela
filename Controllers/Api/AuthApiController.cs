using System.Text;
using System.Text.Encodings.Web;
using Indlela.Data;
using Indlela.Models;
using Indlela.Services.Demo;
using Indlela.Services.Email;
using Indlela.Services.Roadmaps;
using Indlela.ViewModels.Api;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Indlela.Controllers.Api;

/// <summary>
/// Sign-in for the mobile app. Uses ASP.NET Core Identity's built-in bearer tokens (no extra package):
/// login returns { tokenType, accessToken, expiresIn, refreshToken }; the app sends "Authorization: Bearer …".
/// Same rules as the website: verified email required, 5 failures → 15-minute lockout.
/// </summary>
[ApiController, IgnoreAntiforgeryToken]
[Route("api/auth")]
public class AuthApiController(
    UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn,
    IOptionsMonitor<BearerTokenOptions> bearerOptions,
    IAppEmailSender email,
    IWebHostEnvironment env,
    IConfiguration config,
    ILogger<AuthApiController> logger) : ControllerBase
{
    // POST /api/auth/login  → 200 token JSON | 401 | 403 (email not verified) | 423 (locked out)
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null) return Unauthorized(new ApiError("That email and password combination didn't work."));

        var result = await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

        if (result.IsLockedOut)
            return StatusCode(StatusCodes.Status423Locked, new ApiError("Too many attempts. Please wait 15 minutes and try again."));

        if (result.IsNotAllowed)
        {
            // Only reveal the verify-email state once the password is proven correct.
            if (!user.EmailConfirmed && await users.CheckPasswordAsync(user, request.Password))
            {
                var link = DemoMode.IsOn(config, env) ? await BuildConfirmationLinkAsync(user) : null;
                return StatusCode(StatusCodes.Status403Forbidden, new ApiError("Please verify your email first — check your inbox for the link.", link));
            }
            return Unauthorized(new ApiError("That email and password combination didn't work."));
        }

        if (!result.Succeeded) return Unauthorized(new ApiError("That email and password combination didn't work."));

        var principal = await signIn.CreateUserPrincipalAsync(user);
        return SignIn(principal, IdentityConstants.BearerScheme); // the bearer handler writes the token JSON
    }

    // POST /api/auth/refresh  → new token JSON when the refresh token is still valid
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshRequest request)
    {
        var protector = bearerOptions.Get(IdentityConstants.BearerScheme).RefreshTokenProtector;
        var ticket = protector.Unprotect(request.RefreshToken);

        if (ticket?.Properties.ExpiresUtc is not { } expires || DateTimeOffset.UtcNow >= expires
            || await signIn.ValidateSecurityStampAsync(ticket.Principal) is not ApplicationUser user)
            return Unauthorized(new ApiError("Your session has expired. Please log in again."));

        var principal = await signIn.CreateUserPrincipalAsync(user);
        return SignIn(principal, IdentityConstants.BearerScheme);
    }

    // POST /api/auth/register  → 200 { message, demoConfirmationLink } | 400 validation errors
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest model)
    {
        var now = DateTime.UtcNow;
        var user = new ApplicationUser
        {
            UserName = model.Email.Trim(),
            Email = model.Email.Trim(),
            PhoneNumber = AccountController.NormaliseSaMobile(model.MobileNumber),
            FirstName = model.FirstName.Trim(),
            Surname = model.Surname.Trim(),
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
            // Never reveal that an email is already registered.
            if (result.Errors.Any(e => e.Code is nameof(IdentityErrorDescriber.DuplicateUserName) or nameof(IdentityErrorDescriber.DuplicateEmail)))
                ModelState.AddModelError(string.Empty, "We couldn't create an account with those details. If you already have one, log in.");
            foreach (var error in result.Errors.Where(e => !e.Code.StartsWith("Duplicate", StringComparison.Ordinal)))
                ModelState.AddModelError(string.Empty, error.Description);
            return ValidationProblem(ModelState);
        }

        await users.AddToRoleAsync(user, SeedData.LearnerRole);
        var link = await BuildConfirmationLinkAsync(user);
        await email.SendAsync(user.Email!, "Verify your Indlela account",
            AccountController.VerificationEmailBody(user.FirstName, link));

        logger.LogInformation("New learner registered from the mobile app: {UserId}", user.Id);
        return Ok(new RegisterResponse(
            "Account created. Check your email for the verification link, then log in.",
            DemoMode.IsOn(config, env) ? link : null));
    }

    private async Task<string> BuildConfirmationLinkAsync(ApplicationUser user)
    {
        var token = await users.GenerateEmailConfirmationTokenAsync(user);
        var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        return Url.Action(nameof(AccountController.ConfirmEmail), "Account", new { userId = user.Id, code }, Request.Scheme)!;
    }
}

/// <summary>The signed-in learner's basics. Every /api endpoint except auth requires the bearer token.</summary>
[ApiController, IgnoreAntiforgeryToken]
[Authorize(AuthenticationSchemes = ApiAuth.Scheme)]
[Route("api/me")]
public class MeApiController(UserManager<ApplicationUser> users, IRoadmapService roadmaps) : ControllerBase
{
    [HttpGet("")]
    public async Task<ActionResult<MeDto>> Get(CancellationToken ct)
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return Unauthorized();
        var active = await roadmaps.GetActiveSlugAsync(user.Id, ct);
        return new MeDto(user.Id, user.FirstName, user.Surname, user.Email ?? "", active);
    }
}
