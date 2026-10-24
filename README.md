# Indlela

Free Digital Learning Aggregator & Engagement Engine — ASP.NET Core 8 MVC, EF Core + SQL Server, ASP.NET Core Identity.
Design: **Option 1 "Imbizo" (civic light)**.

## Run it

```bash
dotnet restore
dotnet ef migrations add InitialCreate   # needs: dotnet tool install --global dotnet-ef
dotnet run
```

- Connection string `IndlelaDb` in `appsettings.json` points to LocalDB. For Azure SQL, set it with user secrets or app settings.
- In Development the app creates the schema and seeds **demo** courses, providers, badges and Tech Pulse items (all fictional, on example.* domains).
- No email service is wired up. In Development the verification link is shown on the "Check your email" page.
- Tech Pulse: add `News:ApiKey` (NewsAPI.org) via user secrets. Without it the page serves stored items.
- SQL Server full-text search: run `Data/Sql/FullTextIndex.sql`, then set `Search:UseFullText` to `true`. LocalDB has no full-text support, so the default uses indexed `LIKE`.
- Data-protection keys go to `./keys` (git-ignored). They encrypt the ID/passport column, so **back them up**. On Azure, move them to Blob Storage + Key Vault.

## Where the use cases live

| Use case | Code |
|---|---|
| UC-01/02 Register, log in, lockout | `Controllers/AccountController.cs`, `Program.cs` (Identity options) |
| UC-04/05 Catalogue, search, filters, paging | `Services/Catalog/CourseCatalogService.cs`, `CoursesController.Index` |
| UC-07 Course details + earnable badges | `CoursesController.Details` |
| UC-13/14/15 Smart handoff | `Services/Handoff/*` (DeepLink, Extension simulation, Manual) |
| UC-16 Confirm enrollment | `CoursesController.ConfirmEnrollment` |
| UC-17 Progress ledger | `Services/Progress/ProgressService.cs`, `ProgressionController.UpdateProgress` |
| UC-18 Dashboard | `ProgressionController.Dashboard` |
| UC-22/23/24 Tech Pulse (6 h cache, skill chips) | `Services/TechPulse/*`, `TechPulseController` |
| UC-31 Badge engine | `Services/Progress/BadgeEngine.cs` |

## Not built yet

These are the next pieces, in suggested order: password reset and profile/consent editing (UC-02 1a, UC-03), the in-app lesson player and quizzes (UC-28/29), and Hangfire jobs (UC-16 reminders, UC-20 nudges). After that come the extension API with single-use tokens (UC-15), provider sync (UC-30/33) and the admin area (UC-08/11/12/32).

Learner-submitted progress is always recorded as **Self-reported**. Verified `InApp` and `ProviderSync` progress can only be written server-side through `IProgressService`, so learners can't award themselves verified badges.
