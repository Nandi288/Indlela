using Indlela.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Indlela.Data;

/// <summary>
/// Development seed: roles, sample courses, handoff mappings, badges and Tech Pulse items.
/// Providers and accreditation details below are DEMO DATA — replace them with verified records
/// from the ingestion pipeline (UC-09/10/11) before any real learner sees the catalogue.
/// </summary>
public static class SeedData
{
    public const string AdminRole = "Admin";
    public const string LearnerRole = "Learner";

    /// <summary>Real mode: hides anything the demo seeded earlier. Courses are unpublished (never deleted, so learner history stays).</summary>
    public static async Task HideDemoContentAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IndlelaDbContext>();

        var demoCourses = await db.Courses
            .Where(c => c.IsPublished && (c.Provider.StartsWith("Demo ") || c.OriginalUrl.Contains("example.") || c.OriginalUrl.StartsWith("/demo-provider/")))
            .ToListAsync();
        // Courses a roadmap lesson uses (e.g. the C# course) become Indlela Academy courses instead of being hidden.
        var lessonCourseIds = await db.RoadmapSteps.Where(s => s.CourseId != null).Select(s => s.CourseId!.Value).ToListAsync();
        foreach (var c in demoCourses)
        {
            if (lessonCourseIds.Contains(c.Id))
            {
                c.Provider = "Indlela Academy"; c.OriginalUrl = "/paths";
                c.AccreditationBody = null; c.IsAccredited = false; c.NqfLevel = null; c.Credits = null;
            }
            else c.IsPublished = false;
        }

        var demoNews = await db.TechPulseArticles
            .Where(a => a.Title.StartsWith("Demo:") || a.SourceName == "Demo News")
            .ToListAsync();
        db.TechPulseArticles.RemoveRange(demoNews);

        await db.SaveChangesAsync();
    }

    public static async Task InitialiseAsync(IServiceProvider services, bool seedDemoContent)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IndlelaDbContext>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        // Use migrations once you've added them (dotnet ef migrations add InitialCreate);
        // until then, create the schema directly so the app runs on first launch.
        if (db.Database.GetMigrations().Any())
            await db.Database.MigrateAsync();
        else
            await db.Database.EnsureCreatedAsync();

        foreach (var role in new[] { AdminRole, LearnerRole })
        {
            if (!await roles.RoleExistsAsync(role))
                await roles.CreateAsync(new IdentityRole(role));
        }

        await SeedAdminAsync(scope.ServiceProvider);

        if (!seedDemoContent || await db.Courses.AnyAsync()) return;

        var now = DateTime.UtcNow;
        var courses = new List<Course>
        {
            new()
            {
                Title = "Introduction to Programming with C#", Slug = "intro-programming-csharp",
                Provider = "Demo Open University", OriginalUrl = "https://example.org/open/intro-csharp",
                Category = "Programming", NqfLevel = 5, Credits = 12, IsAccredited = true,
                AccreditationBody = "Demo SETA", DeliveryMode = DeliveryMode.InApp, LicenceType = "CC BY 4.0",
                EstimatedHours = 24, IsPublished = true, LastVerifiedAt = now,
                Description = "Variables, control flow, methods and classes in C#, with short quizzes after each lesson."
            },
            new()
            {
                Title = "Cloud Fundamentals", Slug = "cloud-fundamentals",
                Provider = "Demo Global MOOC", OriginalUrl = "https://example.com/courses/cloud-fundamentals",
                Category = "Cloud", DeliveryMode = DeliveryMode.External, LicenceType = "All rights reserved",
                EstimatedHours = 10, IsPublished = true, LastVerifiedAt = now,
                Description = "Core cloud concepts: compute, storage, networking, pricing and the shared responsibility model."
            },
            new()
            {
                Title = "Introduction to Cybersecurity", Slug = "intro-cybersecurity",
                Provider = "Demo SETA Learning Hub", OriginalUrl = "https://example.net/hub/cybersecurity-101",
                Category = "Cybersecurity", NqfLevel = 4, Credits = 8, IsAccredited = true,
                AccreditationBody = "Demo SETA", DeliveryMode = DeliveryMode.External, LicenceType = "All rights reserved",
                EstimatedHours = 16, IsPublished = true, LastVerifiedAt = now,
                Description = "Threats, phishing, passwords and safe practice for individuals and small businesses."
            },
            new()
            {
                Title = "Data Analysis with Spreadsheets", Slug = "data-analysis-spreadsheets",
                Provider = "Demo Open University", OriginalUrl = "https://example.org/open/data-spreadsheets",
                Category = "Data", DeliveryMode = DeliveryMode.InApp, LicenceType = "CC BY-SA 4.0",
                EstimatedHours = 8, IsPublished = true, LastVerifiedAt = now,
                Description = "Clean, sort, chart and summarise data using free spreadsheet tools."
            },
            new()
            {
                Title = "Web Development Basics: HTML & CSS", Slug = "web-basics-html-css",
                Provider = "Demo Global MOOC", OriginalUrl = "https://example.com/courses/html-css",
                Category = "Programming", DeliveryMode = DeliveryMode.External, LicenceType = "All rights reserved",
                EstimatedHours = 12, IsPublished = true, LastVerifiedAt = now,
                Description = "Build and style your first responsive web page."
            },
            new()
            {
                Title = "Networking Essentials", Slug = "networking-essentials",
                Provider = "Demo SETA Learning Hub", OriginalUrl = "https://example.net/hub/networking",
                Category = "Networking", NqfLevel = 5, Credits = 10, IsAccredited = true,
                AccreditationBody = "Demo SETA", DeliveryMode = DeliveryMode.External, LicenceType = "All rights reserved",
                EstimatedHours = 20, IsPublished = true, LastVerifiedAt = now,
                Description = "IP addressing, routing, switching and troubleshooting home and office networks."
            }
        };
        db.Courses.AddRange(courses);

        db.ProviderFormMappings.AddRange(
            new ProviderFormMapping
            {
                ProviderName = "Demo Global MOOC", Strategy = HandoffStrategy.DeepLink,
                RegistrationUrl = "https://example.com/signup",
                FieldMapJson = """{"FirstName":"first_name","Surname":"last_name","Email":"email"}""",
                AllowsTrackingParameter = true
            },
            new ProviderFormMapping
            {
                ProviderName = "Demo SETA Learning Hub", Strategy = HandoffStrategy.Manual,
                RegistrationUrl = "https://example.net/hub/register",
                FieldMapJson = "{}"
            });

        db.Badges.AddRange(
            new Badge
            {
                Name = "First Step", Description = "You logged your first progress on Indlela.",
                Criteria = "Record progress on any course.", RuleType = BadgeRuleType.FirstProgress,
                IconUrl = "/img/badges/first-step.svg"
            },
            new Badge
            {
                Name = "Course Finisher", Description = "You completed a course — verified.",
                Criteria = "Complete one course with in-app assessment or provider-verified progress.",
                RuleType = BadgeRuleType.CoursesCompleted, RequiredCount = 1, IsVerifiedOnly = true,
                IconUrl = "/img/badges/finisher.svg"
            },
            new Badge
            {
                Name = "Cloud Explorer", Description = "Three cloud courses completed.",
                Criteria = "Complete three courses in the Cloud category.",
                RuleType = BadgeRuleType.CoursesCompleted, RequiredCount = 3, RuleCategory = "Cloud",
                IconUrl = "/img/badges/cloud.svg"
            });

        await db.SaveChangesAsync();

        db.TechPulseArticles.AddRange(
            new TechPulseArticle
            {
                Title = "Demo: Banks report rise in phishing attacks targeting mobile users",
                Summary = "Sample headline for development. Replace with the live News API feed.",
                SourceUrl = "https://example.org/news/phishing-rise", SourceName = "Demo News",
                Category = "Cybersecurity", PublishedAt = now.AddHours(-3),
                AlignedCourseId = courses.Single(c => c.Slug == "intro-cybersecurity").Id
            },
            new TechPulseArticle
            {
                Title = "Demo: Cloud skills top the list of in-demand IT roles",
                Summary = "Sample headline for development. Replace with the live News API feed.",
                SourceUrl = "https://example.org/news/cloud-skills-demand", SourceName = "Demo News",
                Category = "Cloud", PublishedAt = now.AddHours(-9),
                AlignedCourseId = courses.Single(c => c.Slug == "cloud-fundamentals").Id
            });

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Creates the admin account from configuration (user secrets): "SeedAdmin": { "Email": "...", "Password": "..." }.
    /// Nothing is created when the section is missing, so no default password ever ships in code.
    /// </summary>
    private static async Task SeedAdminAsync(IServiceProvider services)
    {
        var config = services.GetRequiredService<IConfiguration>();
        var email = config["SeedAdmin:Email"];
        var password = config["SeedAdmin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) return;

        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("SeedData");

        var admin = await users.FindByEmailAsync(email);
        if (admin is null)
        {
            var now = DateTime.UtcNow;
            admin = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FirstName = "Admin",
                Surname = "Indlela",
                ConsentTimestamps = new ConsentTimestamps { TermsAcceptedAt = now, PrivacyNoticeAcceptedAt = now }
            };
            var created = await users.CreateAsync(admin, password);
            if (!created.Succeeded)
            {
                logger.LogWarning("Could not create the seed admin: {Errors}",
                    string.Join("; ", created.Errors.Select(e => e.Description)));
                return;
            }
            logger.LogInformation("Seed admin account created for {Email}", email);
        }

        if (!await users.IsInRoleAsync(admin, AdminRole))
            await users.AddToRoleAsync(admin, AdminRole);
    }
}
