using Indlela.Models;
using Microsoft.EntityFrameworkCore;

namespace Indlela.Data;

/// <summary>
/// Seeds the three starter roadmaps (Software Dev, Digital Marketing, Accounting &amp; Finance) and the
/// free Indlela Academy lessons they use. Safe to run on every start-up: it only adds what is missing
/// (matched by slug) and never overwrites rows an admin has edited.
/// </summary>
public static class RoadmapSeed
{
    private const string Academy = "Indlela Academy";

    public static async Task ApplyAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IndlelaDbContext>();
        var log = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("RoadmapSeed");

        // ---------- 1. Indlela Academy lessons (text + quiz in Services/Demo/DemoCourseContent.cs) ----------
        var academyCourses = new[]
        {
            AcademyCourse("intro-programming-csharp", "Introduction to Programming with C#", "Programming", 6,
                "Variables, decisions and loops in C#, the language behind ASP.NET and Unity. Four short lessons, each with a quiz."),
            AcademyCourse("aspnet-core-web-apis", "Build Web APIs with ASP.NET Core", "Programming", 5,
                "What an API is, HTTP verbs and status codes, and saving data with Entity Framework Core."),
            AcademyCourse("digital-marketing-fundamentals", "Digital Marketing Fundamentals", "Marketing", 4,
                "The marketing funnel, SMART goals you can measure, and marketing within POPIA."),
            AcademyCourse("social-media-content", "Social Media Content That Works", "Marketing", 4,
                "Content pillars, planning with a content calendar, and reading your analytics."),
            AcademyCourse("bookkeeping-basics", "Bookkeeping Basics", "Finance", 4,
                "The accounting equation, double-entry bookkeeping and the source documents behind every entry."),
            AcademyCourse("reading-financial-statements", "Reading Financial Statements", "Finance", 4,
                "The income statement, the balance sheet, and why profit is not the same as cash."),
        };

        var slugs = academyCourses.Select(c => c.Slug).ToList();
        var existing = await db.Courses.Where(c => slugs.Contains(c.Slug)).Select(c => c.Slug).ToListAsync(ct);
        var newCourses = academyCourses.Where(c => !existing.Contains(c.Slug)).ToList();
        if (newCourses.Count > 0)
        {
            db.Courses.AddRange(newCourses);
            await db.SaveChangesAsync(ct);
            log.LogInformation("Roadmaps: added {Count} Indlela Academy course(s).", newCourses.Count);
        }

        var courseIds = await db.Courses.Where(c => slugs.Contains(c.Slug))
            .ToDictionaryAsync(c => c.Slug, c => c.Id, ct);

        // ---------- 2. The three roadmaps ----------
        var paths = new List<LearningPath>
        {
            new()
            {
                Slug = "software-dev", Name = "Software Development", Stream = "Tech", SortOrder = 1,
                Summary = "Go from your first line of C# to a small web API you can show an employer.",
                Careers = "Junior developer|Software support technician|QA tester|IT learnership",
                Steps =
                {
                    Lesson(1, "Foundations", "Programming basics in C#",
                        "Variables, types, if-statements and loops — enough to read and write small programs.",
                        courseIds["intro-programming-csharp"], 6, "C# basics|Problem solving"),
                    External(2, "Foundations", "Foundational C# with Microsoft",
                        "Practise C# with guided projects and earn the free freeCodeCamp + Microsoft certification.",
                        "https://www.freecodecamp.org/learn/foundational-c-sharp-with-microsoft/", "freeCodeCamp",
                        "Free, including the certification", 35, "C#|Debugging|Methods and arrays", "Certificate link"),
                    Lesson(3, "Build skills", "How web APIs work",
                        "What an API does, HTTP verbs and status codes, and how EF Core saves data to a database.",
                        courseIds["aspnet-core-web-apis"], 5, "REST APIs|HTTP|Entity Framework Core"),
                    External(4, "Build skills", "Create a web API with ASP.NET Core controllers",
                        "Build and test a real API step by step in Microsoft's beginner module.",
                        "https://learn.microsoft.com/en-us/training/modules/build-web-api-aspnet-core/", "Microsoft Learn",
                        "Free", 1, "ASP.NET Core|Web APIs|Testing endpoints", "Microsoft Learn profile link"),
                    Capstone(5, "Build a small to-do or stock API",
                        "Build an API with at least three endpoints that saves to a database, put the code on GitHub and write a short README.",
                        "Portfolio project|Git and GitHub|ASP.NET Core"),
                }
            },
            new()
            {
                Slug = "digital-marketing", Name = "Digital Marketing", Stream = "Business", SortOrder = 2,
                Summary = "Learn how brands find customers online, then plan a real campaign for a local business.",
                Careers = "Social media assistant|Digital marketing intern|Content creator|Marketing learnership",
                Steps =
                {
                    Lesson(1, "Foundations", "Digital marketing fundamentals",
                        "The marketing funnel, SMART goals and what POPIA means for marketing messages.",
                        courseIds["digital-marketing-fundamentals"], 4, "Marketing funnel|SMART goals|POPIA basics"),
                    Lesson(2, "Build skills", "Social media content that works",
                        "Content pillars, a weekly content calendar and reading reach and engagement.",
                        courseIds["social-media-content"], 4, "Content planning|Social media analytics"),
                    External(3, "Build skills", "HubSpot Academy: Digital Marketing",
                        "Go deeper on SEO, social, email and paid ads, and earn HubSpot's free certificate.",
                        "https://academy.hubspot.com/courses/digital-marketing", "HubSpot Academy",
                        "Free, including the certificate", 5, "SEO|Email marketing|Paid ads", "Certificate link"),
                    Capstone(4, "Plan a month of content for a local business",
                        "Pick a spaza shop, salon or church group. Write their goals, a 4-week content calendar and 3 sample posts, and share it as a Google Doc or Canva link.",
                        "Campaign planning|Copywriting|Portfolio project"),
                }
            },
            new()
            {
                Slug = "accounting-finance", Name = "Accounting & Finance", Stream = "Finance", SortOrder = 3,
                Summary = "Understand the books of a business and read its financial statements with confidence.",
                Careers = "Bookkeeping clerk|Accounts assistant|Payroll assistant|Finance learnership",
                Steps =
                {
                    Lesson(1, "Foundations", "Bookkeeping basics",
                        "The accounting equation, debits and credits, and the documents behind every entry.",
                        courseIds["bookkeeping-basics"], 4, "Double-entry bookkeeping|Source documents"),
                    Lesson(2, "Build skills", "Reading financial statements",
                        "The income statement, the balance sheet and the difference between profit and cash.",
                        courseIds["reading-financial-statements"], 4, "Income statement|Balance sheet|Cash flow"),
                    External(3, "Build skills", "Introduction to Financial Accounting (Wharton)",
                        "A university-level course from the University of Pennsylvania on Coursera.",
                        "https://www.coursera.org/learn/wharton-accounting", "Coursera · Wharton",
                        "Free to enrol and learn; the certificate may cost (financial aid available)", 20,
                        "Financial accounting|Accruals|Ratio basics", "Certificate or progress link"),
                    Capstone(4, "Keep the books for one month",
                        "Record a month of transactions for a real or made-up small business in a spreadsheet, then produce a simple income statement and balance sheet.",
                        "Spreadsheets|Bookkeeping|Portfolio project"),
                }
            },
        };

        var pathSlugs = await db.LearningPaths.Select(p => p.Slug).ToListAsync(ct);
        var newPaths = paths.Where(p => !pathSlugs.Contains(p.Slug)).ToList();
        if (newPaths.Count > 0)
        {
            db.LearningPaths.AddRange(newPaths);
            await db.SaveChangesAsync(ct);
            log.LogInformation("Roadmaps: added {Count} learning path(s).", newPaths.Count);
        }

        // ---------- 3. Job roles for Job Matching + starter badges ----------
        await CareerSeed.ApplyAsync(db, ct);
    }

    private static Course AcademyCourse(string slug, string title, string category, int hours, string description) => new()
    {
        Slug = slug,
        Title = title,
        Provider = Academy,
        OriginalUrl = "/paths", // internal: LinkChecker treats "/" links as always up
        Category = category,
        Description = description,
        DeliveryMode = DeliveryMode.InApp,
        LicenceType = "CC BY 4.0",
        IsFree = true,
        IsAccredited = false,
        EstimatedHours = hours,
        IsPublished = true,
        LastVerifiedAt = DateTime.UtcNow
    };

    private static RoadmapStep Lesson(int order, string stage, string title, string learn, int courseId, int hours, string skills) => new()
    {
        Order = order, Stage = stage, Title = title, WhatYouLearn = learn,
        Type = RoadmapStepType.IndlelaLesson, CourseId = courseId, ProviderName = Academy,
        CostNote = "Free", EstimatedHours = hours, Skills = skills, ProofType = "Indlela quiz"
    };

    private static RoadmapStep External(int order, string stage, string title, string learn, string url, string provider,
        string cost, int hours, string skills, string proof) => new()
    {
        Order = order, Stage = stage, Title = title, WhatYouLearn = learn,
        Type = RoadmapStepType.ExternalCourse, ExternalUrl = url, ProviderName = provider,
        CostNote = cost, EstimatedHours = hours, Skills = skills, ProofType = proof
    };

    private static RoadmapStep Capstone(int order, string title, string learn, string skills) => new()
    {
        Order = order, Stage = "Prove it", Title = title, WhatYouLearn = learn,
        Type = RoadmapStepType.Capstone, ProviderName = "Your own project",
        CostNote = "Free", EstimatedHours = 10, Skills = skills, ProofType = "Project link"
    };
}
