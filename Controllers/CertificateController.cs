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
    /// <summary>SVG QR code of <see cref="VerifyUrl"/>; scanning it opens the public verification page.</summary>
    public string QrSvg { get; init; } = "";
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

    // Short, typeable certificate codes: 32 characters with no look-alikes (no 0/O, 1/I).
    private const string CodeAlphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";
    private static readonly System.Text.RegularExpressions.Regex ShortCodePattern =
        new("^[2-9A-HJ-NP-Z]{4}-?[2-9A-HJ-NP-Z]{4}-?[2-9A-HJ-NP-Z]{2}$", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>Public verification. Accepts the short code (from the QR or typed by an employer) and older long links.</summary>
    [AllowAnonymous, HttpGet("verify/{code?}")]
    public async Task<IActionResult> Verify(string? code, CancellationToken ct)
    {
        code = (code ?? Request.Query["code"].ToString()).Trim();
        if (code.Length == 0) return View("Verify", null);

        int? id = null;
        var candidate = code.ToUpperInvariant().Replace(" ", "");
        if (ShortCodePattern.IsMatch(candidate))
        {
            var normalised = Format(candidate.Replace("-", ""));
            id = await db.LearnerEnrollments.AsNoTracking()
                .Where(e => e.CertificateCode == normalised && e.CompletedAt != null)
                .Select(e => (int?)e.Id).FirstOrDefaultAsync(ct);
        }
        else
        {
            try
            {
                if (int.TryParse(Protector.Unprotect(code), out var parsed)) id = parsed; // links issued before short codes
            }
            catch (System.Security.Cryptography.CryptographicException)
            {
                // Tampered or made-up code: falls through to "not valid".
            }
        }

        var vm = id is int enrollmentId ? await BuildAsync(enrollmentId, ownerUserId: null, ct) : null;
        ViewData["TriedCode"] = code.Length > 40 ? null : code;
        return View("Verify", vm);
    }

    private async Task<CertificateVm?> BuildAsync(int enrollmentId, string? ownerUserId, CancellationToken ct)
    {
        var row = await db.LearnerEnrollments.AsNoTracking()
            .Where(e => e.Id == enrollmentId && e.CompletedAt != null && (ownerUserId == null || e.UserId == ownerUserId))
            .Select(e => new
            {
                e.Id, e.CompletedAt, e.CompletionSource, e.CertificateCode,
                e.User!.FirstName, e.User.Surname,
                e.Course!.Title, e.Course.Provider, e.Course.Category, e.Course.EstimatedHours
            })
            .SingleOrDefaultAsync(ct);
        if (row is null) return null;

        var certCode = row.CertificateCode ?? await AssignCodeAsync(row.Id, ct);
        var verifyUrl = Url.Action(nameof(Verify), "Certificate", new { code = certCode }, Request.Scheme)!;
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
            VerifyUrl = verifyUrl,
            QrSvg = Indlela.Services.Certificates.QrCode.Encode(verifyUrl).ToSvg(title: "Scan to verify this certificate"),
            ShortCode = certCode,
            IsOwnerView = ownerUserId is not null
        };
    }

    /// <summary>Gives a completed enrolment its permanent random code the first time its certificate is shown.</summary>
    private async Task<string> AssignCodeAsync(int enrollmentId, CancellationToken ct)
    {
        var enrollment = await db.LearnerEnrollments.FirstAsync(e => e.Id == enrollmentId, ct);
        for (var attempt = 0; ; attempt++)
        {
            enrollment.CertificateCode = NewCode();
            try
            {
                await db.SaveChangesAsync(ct);
                return enrollment.CertificateCode;
            }
            catch (DbUpdateException) when (attempt < 3)
            {
                // Two certificates drew the same code (about 1 in a quadrillion): draw again.
            }
        }
    }

    private static string NewCode()
    {
        Span<char> chars = stackalloc char[10];
        for (var i = 0; i < chars.Length; i++)
            chars[i] = CodeAlphabet[System.Security.Cryptography.RandomNumberGenerator.GetInt32(CodeAlphabet.Length)];
        return Format(new string(chars));
    }

    private static string Format(string tenChars) => $"{tenChars[..4]}-{tenChars[4..8]}-{tenChars[8..]}";
}
