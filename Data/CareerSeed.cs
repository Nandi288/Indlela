using Indlela.Models;
using Microsoft.EntityFrameworkCore;

namespace Indlela.Data;

/// <summary>
/// Entry-level job roles for Job Matching, plus the starter badges. Runs on every start-up (called from
/// <see cref="RoadmapSeed"/>) and only adds what is missing. Skill names match the skills on roadmap steps exactly.
/// </summary>
public static class CareerSeed
{
    public static async Task ApplyAsync(IndlelaDbContext db, CancellationToken ct = default)
    {
        var roles = new (string Slug, string Title, string Field, string Summary, string Types, string[] Skills)[]
        {
            ("junior-web-developer", "Junior Web Developer", "Tech",
                "Builds and fixes websites and web apps as part of a development team.",
                "Junior jobs, internships, IT learnerships",
                ["C# basics", "ASP.NET Core", "REST APIs", "Git and GitHub", "Problem solving"]),
            ("backend-developer-intern", "Backend Developer Intern", "Tech",
                "Helps build the server side of apps: APIs, databases and business rules.",
                "Internships, graduate programmes",
                ["C#", "Web APIs", "REST APIs", "Entity Framework Core", "HTTP"]),
            ("software-tester", "Junior Software Tester", "Tech",
                "Tests apps before they reach users, finds bugs and checks that fixes work.",
                "Junior jobs, QA learnerships",
                ["Problem solving", "Debugging", "Testing endpoints", "HTTP"]),
            ("social-media-assistant", "Social Media Assistant", "Business",
                "Plans and posts content for a business and reports on what worked.",
                "Junior jobs, internships, freelance work",
                ["Content planning", "Social media analytics", "Copywriting", "POPIA basics"]),
            ("digital-marketing-intern", "Digital Marketing Intern", "Business",
                "Supports campaigns across search, email and social media.",
                "Internships, marketing learnerships",
                ["Marketing funnel", "SMART goals", "SEO", "Email marketing", "Paid ads"]),
            ("content-creator", "Content Creator", "Business",
                "Writes and designs posts, newsletters and campaign content for brands.",
                "Freelance work, junior jobs",
                ["Copywriting", "Content planning", "Campaign planning"]),
            ("bookkeeping-clerk", "Bookkeeping Clerk", "Finance",
                "Captures transactions and keeps a small business's books up to date.",
                "Junior jobs, finance learnerships",
                ["Double-entry bookkeeping", "Source documents", "Spreadsheets", "Bookkeeping"]),
            ("accounts-assistant", "Accounts Assistant", "Finance",
                "Helps prepare statements, reconcile accounts and track cash flow.",
                "Junior jobs, internships",
                ["Income statement", "Balance sheet", "Cash flow", "Double-entry bookkeeping"]),
            ("finance-learner", "Finance Learnership", "Finance",
                "A paid 12-month learnership in a finance team, with study and work combined.",
                "Learnerships",
                ["Financial accounting", "Accruals", "Ratio basics", "Bookkeeping"]),
        };

        var existing = await db.JobRoles.Select(r => r.Slug).ToListAsync(ct);
        var order = 0;
        foreach (var r in roles)
        {
            order++;
            if (existing.Contains(r.Slug)) continue;
            var role = new JobRole { Slug = r.Slug, Title = r.Title, Field = r.Field, Summary = r.Summary, OpeningTypes = r.Types, SortOrder = order };
            foreach (var skill in r.Skills) role.Skills.Add(new JobRoleSkill { Skill = skill });
            db.JobRoles.Add(role);
        }

        // Starter badges (demo mode seeds these too; only added when there are none).
        if (!await db.Badges.AnyAsync(ct))
        {
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
                    Name = "Three in a Row", Description = "Three courses completed.",
                    Criteria = "Complete any three courses.",
                    RuleType = BadgeRuleType.CoursesCompleted, RequiredCount = 3,
                    IconUrl = "/img/badges/finisher.svg"
                });
        }

        await db.SaveChangesAsync(ct);
    }
}
