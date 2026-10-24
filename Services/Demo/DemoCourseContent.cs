namespace Indlela.Services.Demo;

public sealed record DemoLesson(string Title, string[] Paragraphs, string? Question = null, string[]? Options = null, int CorrectIndex = 0, string? Explanation = null)
{
    public bool HasQuiz => Question is not null && Options is { Length: > 1 };
}

/// <summary>
/// In-app lesson content for the demo (UC-28/29). In production this comes from the content import pipeline
/// (UC-27: CourseModule → Lesson → QuizItem tables, reviewed in UC-32). Kept in code so the demo needs no migration.
/// </summary>
public static class DemoCourseContent
{
    private static readonly Dictionary<string, DemoLesson[]> Lessons = new(StringComparer.OrdinalIgnoreCase)
    {
        ["intro-programming-csharp"] =
        [
            new("What a program is",
                ["A program is a list of instructions a computer follows exactly, in order. C# is a language for writing those instructions, and it powers everything from banking apps to games built with Unity.",
                 "Every C# program starts somewhere. In a modern console app that is simply the first line of Program.cs. The line Console.WriteLine(\"Sawubona!\"); prints a greeting to the screen."],
                "What does Console.WriteLine(\"Sawubona!\"); do?",
                ["Saves a file called Sawubona", "Prints Sawubona! on the screen", "Asks the user for their name"], 1,
                "WriteLine writes text to the console (the screen), then moves to a new line."),
            new("Variables hold values",
                ["A variable is a named box that holds a value. In C# you say what kind of value it holds: int for whole numbers, double for decimals, string for text and bool for true/false.",
                 "Example: int age = 19; string city = \"Durban\"; bool isStudent = true;"],
                "Which type would you use to store the price R149.99?",
                ["int", "string", "double"], 2,
                "Prices have decimals, so a decimal-number type like double (or decimal, for money) is right. int only holds whole numbers."),
            new("Making decisions with if",
                ["Programs make choices with if and else. The code inside the if block only runs when the condition is true.",
                 "Example: if (marks >= 50) { Console.WriteLine(\"Pass\"); } else { Console.WriteLine(\"Try again\"); }"],
                "If marks is 72, what does that example print?",
                ["Pass", "Try again", "Nothing"], 0,
                "72 is greater than or equal to 50, so the condition is true and the if block runs."),
            new("Repeating with loops",
                ["A loop repeats code so you don't have to copy it. A for loop counts: for (int i = 1; i <= 3; i++) { Console.WriteLine(i); } prints 1, 2 and 3.",
                 "Loops plus variables plus if statements are the building blocks of almost every program you will ever write."],
                "How many times does for (int i = 0; i < 5; i++) run?",
                ["4", "5", "6"], 1,
                "It starts at 0 and stops before 5: 0, 1, 2, 3, 4 — that's five runs.")
        ],
        ["data-analysis-spreadsheets"] =
        [
            new("Clean data first",
                ["Most time in data work goes into cleaning: removing duplicates, fixing spelling, and making sure each column holds one kind of value.",
                 "A tidy table has one row per record and one column per field — for example one row per learner, with columns for name, province and marks."],
                "Which of these is a tidy column?",
                ["\"Durban, 72%\"", "Province", "Notes and marks and dates"], 1,
                "A tidy column holds one kind of value. Mixing a city and a mark in one cell makes it hard to sort or count."),
            new("Sort, filter and count",
                ["Sorting puts rows in order; filtering hides rows you don't need. The COUNTIF formula counts rows that match a rule: =COUNTIF(C2:C200, \"KwaZulu-Natal\").",
                 "SUM, AVERAGE, MIN and MAX answer most first questions about a data set."],
                "Which formula gives the average of cells B2 to B50?",
                ["=SUM(B2:B50)", "=AVERAGE(B2:B50)", "=COUNT(B2:B50)"], 1,
                "AVERAGE adds the values and divides by how many there are."),
            new("Tell the story with a chart",
                ["Pick the chart for the question: a bar chart compares groups, a line chart shows change over time, and a pie chart is only for parts of one whole.",
                 "Always give the chart a clear title and label the axes, so someone can read it without you there."],
                "You want to show monthly sign-ups over a year. Which chart?",
                ["Line chart", "Pie chart", "No chart — just a table"], 0,
                "Change over time is exactly what a line chart is for.")
        ],
        ["aspnet-core-web-apis"] =
        [
            new("What a Web API does",
                ["A Web API lets other apps (a website, a phone app, a .NET MAUI app) ask your server for data over HTTP and get JSON back.",
                 "In ASP.NET Core you build one with a controller that inherits from ControllerBase and is marked [ApiController]."],
                "What does an [ApiController] action usually send back for a C# object?",
                ["An HTML page", "JSON", "A Word document"], 1,
                "ASP.NET Core turns returned objects into JSON by default, which any app can read."),
            new("HTTP verbs and status codes",
                ["Each HTTP verb has a job: GET reads, POST creates, PUT replaces and DELETE removes.",
                 "Good APIs answer with clear status codes: 200 OK, 201 Created, 404 Not Found when something doesn't exist."],
                "Which verb should an endpoint use to create a new enrolment?",
                ["GET", "POST", "DELETE"], 1,
                "POST creates a new resource; GET only reads."),
            new("Saving data with EF Core",
                ["Entity Framework Core maps your C# classes to SQL Server tables and turns LINQ queries into SQL.",
                 "In Visual Studio, Add-Migration and Update-Database create and update the tables from your classes."],
                "What does EF Core do with db.Courses.Where(c => c.IsPublished)?",
                ["Runs it in the browser", "Translates it to SQL and runs it on SQL Server", "Deletes unpublished courses"], 1,
                "EF Core turns LINQ into a SQL WHERE clause, so filtering happens in the database.")
        ],
        ["digital-marketing-fundamentals"] =
        [
            new("The marketing funnel",
                ["The funnel follows a customer from Awareness (they hear of you) to Consideration (they compare), Conversion (they buy or sign up) and Loyalty (they come back and recommend you).",
                 "Each stage needs different content: awareness posts reach new people, while offers and reviews help people decide."],
                "At which stage does the customer buy?",
                ["Awareness", "Conversion", "Loyalty"], 1,
                "Conversion is when interest turns into a purchase or sign-up."),
            new("Goals you can measure",
                ["Vague goals like \"get more likes\" are hard to act on. SMART goals are Specific, Measurable, Achievable, Relevant and Time-bound.",
                 "Example: grow Instagram followers for a Durban salon by 20% in three months."],
                "Which of these is a SMART goal?",
                ["Be famous online", "Grow followers by 20% in 3 months", "Post sometimes"], 1,
                "It says exactly what, how much and by when."),
            new("Marketing and POPIA",
                ["In South Africa, POPIA applies to marketing. Generally you need a person's consent before sending them direct marketing by email or SMS (existing customers are treated differently).",
                 "Every marketing message must give people an easy way to opt out."],
                "Before emailing marketing to a new contact, what do you generally need?",
                ["Nothing", "Their consent", "Their ID number"], 1,
                "Direct electronic marketing generally needs consent, plus a clear opt-out.")
        ],
        ["social-media-content"] =
        [
            new("Content pillars",
                ["Strong brands post around three to five content pillars: themes like tips, behind the scenes, customer stories and offers.",
                 "Pillars keep posts consistent and stop you running out of ideas."],
                "What are content pillars?",
                ["Paid adverts", "A few recurring themes you post about", "Hashtag lists"], 1,
                "Pillars are the core themes that give a brand's content a consistent shape."),
            new("Plan with a calendar",
                ["A content calendar plans posts weeks ahead, so you post regularly instead of only when you remember.",
                 "Plan around local moments too: month-end, school holidays, Heritage Day."],
                "What does a content calendar help you do?",
                ["Post consistently with a plan", "Buy followers", "Avoid analytics"], 0,
                "Planning ahead keeps posting regular and on-message."),
            new("Read your analytics",
                ["Engagement rate (likes, comments, shares and saves divided by reach) shows how many people cared about a post.",
                 "Repeat what works and test one change at a time, such as a new posting time or format."],
                "What does engagement rate compare?",
                ["Interactions to reach", "Followers to following", "Posts to days"], 0,
                "It shows what share of the people who saw a post interacted with it.")
        ],
        ["bookkeeping-basics"] =
        [
            new("The accounting equation",
                ["Every business runs on one equation: Assets = Liabilities + Equity. What the business owns is funded by what it owes or by what the owners put in.",
                 "Example: a spaza shop's stock and cash (assets) are funded by a supplier account (liability) and the owner's savings (equity)."],
                "What is the accounting equation?",
                ["Assets = Liabilities + Equity", "Profit = Assets", "Cash = Debt"], 0,
                "It's the foundation every balance sheet follows."),
            new("Double-entry bookkeeping",
                ["Double-entry records every transaction in at least two accounts, so total debits always equal total credits.",
                 "Buying stock for cash increases inventory and decreases cash by the same amount."],
                "In double-entry bookkeeping, total debits must…",
                ["equal total credits", "be double the credits", "always be zero"], 0,
                "Each transaction is recorded twice, so the books balance."),
            new("Source documents",
                ["Every entry needs proof: an invoice, receipt or bank statement. Keep them, even as phone photos.",
                 "Keep business and personal money in separate accounts. It makes tax time and getting funding far easier."],
                "Which document proves a sale to a customer?",
                ["An invoice", "A tweet", "A birthday card"], 0,
                "Invoices and receipts are the evidence behind sales entries.")
        ],
        ["reading-financial-statements"] =
        [
            new("The income statement",
                ["The income statement shows performance over a period: revenue minus expenses equals profit (or loss).",
                 "It answers: did the business make money this month or year?"],
                "Which statement shows profit over a period?",
                ["Income statement", "Balance sheet", "Payslip"], 0,
                "Revenue minus expenses over the period gives profit."),
            new("The balance sheet",
                ["The balance sheet is a snapshot on one date: assets, liabilities and equity.",
                 "It answers: what does the business own and owe right now?"],
                "What does the balance sheet show?",
                ["Assets, liabilities and equity on a date", "Only sales", "Next year's budget"], 0,
                "It's a snapshot of the financial position on one day."),
            new("Profit isn't cash",
                ["The cash flow statement shows cash actually coming in and going out.",
                 "A business can be profitable on paper and still run out of cash if customers pay late or too much money is tied up in stock."],
                "Can a profitable business run out of cash?",
                ["Yes, for example if customers pay late", "No, never", "Only banks can"], 0,
                "Profit is earned on paper; cash depends on when money actually moves.")
        ]
    };

    /// <summary>Lessons for a course; openly licensed courses without imported content get a short generic path.</summary>
    public static DemoLesson[] For(string slug, string title, string? description, string provider)
    {
        if (Lessons.TryGetValue(slug, out var lessons)) return lessons;
        return
        [
            new($"Welcome to {title}",
                [description ?? $"This openly licensed course from {provider} is being prepared for in-app learning.",
                 "In the full system the lessons, videos and quizzes are imported from the source (UC-27) and reviewed by an admin (UC-32) before they appear here."]),
            new("Study the source material",
                [$"Work through the original course material from {provider}. Every lesson in Indlela keeps the author's credit and a link to the original, as the licence requires."]),
            new("Check your understanding",
                ["Reflect on what you learned and mark this lesson complete to finish the course."])
        ];
    }
}
