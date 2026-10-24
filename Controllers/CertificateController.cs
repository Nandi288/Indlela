using Indlela.Data;
using Indlela.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Indlela.Controllers;

public class CertificateVm
{
    public string LearnerName { get; init; } = "";
    public string CourseTitle { get; init; } = "";
    public string Provider { get; init; } = "";
    public string Category { get; init; } = "";
    public int? EstimatedHours { get; init; }
    public DateTime CompletedAt { get; init; }
    public bool IsVerified { get; init; }
    public string VerifiedBy { get; init; } = "";
    public string VerifyUrl { get; init; } = "";
    public string ShortCode { get; init; } = "";
    public bool IsOwnerView { get; init; }
}

/// <summary>
/// Certificate of achievement for a completed course, with a public verification link (UC-31 sharing).
/// The verification code is an encrypted, tamper-proof token of the enrolment id, so no new table is needed.
/// </summary>
[Route("certificate")]
public class CertificateController(IndlelaDbContext db, IDataProtectionProvider protection, UserManager<ApplicationUser> users) : Controller
{
    private IDataProtector Protector => protection.CreateProtector("Indlela.Certificates.v1");

    [Authorize, HttpGet("{enrollmentId:int}")]
    public async Task<IActionResult> Show(int enrollmentId, CancellationToken ct)
    {
        var userId = users.GetUserId(User);
        var vm = await BuildAsync(enrollmentId, userId, ct);
        if (vm is null)
        {
            TempData["Flash"] = "Your certificate appears here once you finish the course.";
            return Redirect("/dashboard");
        }
        return View("Show", vm);
    }

    [AllowAnonymous, HttpGet("verify/{code}")]
    public async Task<IActionResult> Verify(string code, CancellationToken ct)
    {
        int? id = null;
        try
        {
            if (int.TryParse(Protector.Unprotect(code), out var parsed)) id = parsed;
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            // Tampered or made-up code: falls through to "not valid".
        }

        var vm = id is int enrollmentId ? await BuildAsync(enrollmentId, ownerUserId: null, ct) : null;
        return View("Verify", vm);
    }

    private async Task<CertificateVm?> BuildAsync(int enrollmentId, string? ownerUserId, CancellationToken ct)
    {
        var row = await db.LearnerEnrollments.AsNoTracking()
            .Where(e => e.Id == enrollmentId && e.CompletedAt != null && (ownerUserId == null || e.UserId == ownerUserId))
            .Select(e => new
            {
                e.Id, e.CompletedAt, e.CompletionSource,
                e.User!.FirstName, e.User.Surname,
                e.Course!.Title, e.Course.Provider, e.Course.Category, e.Course.EstimatedHours
            })
            .SingleOrDefaultAsync(ct);
        if (row is null) return null;

        var code = Protector.Protect(row.Id.ToString());
        var verified = row.CompletionSource is ProgressSource.InApp or ProgressSource.ProviderSync;
        return new CertificateVm
        {
            LearnerName = $"{row.FirstName} {row.Surname}".Trim(),
            CourseTitle = row.Title,
            Provider = row.Provider,
            Category = row.Category,
            EstimatedHours = row.EstimatedHours,
            CompletedAt = row.CompletedAt!.Value,
            IsVerified = verified,
            VerifiedBy = row.CompletionSource switch
            {
                ProgressSource.InApp => "Verified by Indlela in-app assessments",
                ProgressSource.ProviderSync => $"Verified by live progress from {row.Provider}",
                _ => "Self-reported by the learner (not verified)"
            },
            VerifyUrl = Url.Action(nameof(Verify), "Certificate", new { code }, Request.Scheme)!,
            ShortCode = $"IND-{row.Id:D5}",
            IsOwnerView = ownerUserId is not null
        };
    }
}
