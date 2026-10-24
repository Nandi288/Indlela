using System.Security.Claims;
using Indlela.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Indlela.Services;

/// <summary>
/// Adds the learner's first name to the sign-in cookie, so the app shell can greet them on every page
/// without a database query. Learners signed in before this was added get it at their next login.
/// </summary>
public sealed class IndlelaClaimsPrincipalFactory(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>(userManager, roleManager, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        if (!string.IsNullOrWhiteSpace(user.FirstName))
            identity.AddClaim(new Claim(ClaimTypes.GivenName, user.FirstName));
        return identity;
    }
}
