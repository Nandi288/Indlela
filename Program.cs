using Indlela.Data;
using Indlela.Models;
using Indlela.Services;
using Indlela.Services.Catalog;
using Indlela.Services.Dashboard;
using Indlela.Services.Email;
using Indlela.Services.Handoff;
using Indlela.Services.Ingestion;
using Indlela.Services.Metrics;
using Indlela.Services.Progress;
using Indlela.Services.TechPulse;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// ---------- Data protection ----------
// Encrypts the ID/passport column and Identity tokens. Keys MUST persist, or encrypted IDs become unreadable
// after a restart. On Azure, use PersistKeysToAzureBlobStorage + ProtectKeysWithAzureKeyVault instead.
builder.Services.AddDataProtection()
    .SetApplicationName("Indlela")
    .PersistKeysToFileSystem(new DirectoryInfo(
        config["DataProtection:KeysPath"] is { Length: > 0 } keysPath
            ? keysPath
            : Path.Combine(builder.Environment.ContentRootPath, "keys")));

// ---------- EF Core + SQL Server ----------
builder.Services.AddDbContext<IndlelaDbContext>(options =>
    options.UseSqlServer(
        config.GetConnectionString("IndlelaDb")
            ?? throw new InvalidOperationException("Connection string 'IndlelaDb' not found."),
        sql => sql.EnableRetryOnFailure(maxRetryCount: 5))); // Azure SQL transient faults

// ---------- Identity (UC-01, UC-02) ----------
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.SignIn.RequireConfirmedEmail = true;
        options.User.RequireUniqueEmail = true;

        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false; // friendlier on phone keyboards; length does the work

        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddEntityFrameworkStores<IndlelaDbContext>()
    .AddDefaultTokenProviders()
    .AddClaimsPrincipalFactory<IndlelaClaimsPrincipalFactory>(); // first name in the cookie for the app shell

// Verification links last 24 hours (UC-01 alt 5a).
builder.Services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromHours(24));

builder.Services.ConfigureApplicationCookie(o =>
{
    o.LoginPath = "/account/login";
    o.LogoutPath = "/account/logout";
    o.AccessDeniedPath = "/account/access-denied";
    o.SlidingExpiration = true;
    o.Cookie.HttpOnly = true;
    o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

// ---------- Mobile app sign-in (Indlela.Mobile → /api/*) ----------
// Identity's built-in bearer tokens (part of ASP.NET Core 8, no extra package). Tokens are protected with the
// same Data Protection keys as the cookies, so the keys/ folder must persist. Website pages still use cookies.
builder.Services.AddAuthentication()
    .AddBearerToken(IdentityConstants.BearerScheme, o =>
    {
        o.BearerTokenExpiration = TimeSpan.FromHours(1);
        o.RefreshTokenExpiration = TimeSpan.FromDays(14);
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("Admin", p => p.RequireRole(SeedData.AdminRole));

// ---------- Application services ----------
builder.Services.AddMemoryCache();

builder.Services.AddScoped<ICourseCatalogService, CourseCatalogService>();
builder.Services.AddScoped<IImpactMetricsService, ImpactMetricsService>();

builder.Services.AddScoped<IHandoffStrategy, DeepLinkStrategy>();
builder.Services.AddScoped<IHandoffStrategy, ExtensionStrategy>();
builder.Services.AddScoped<IHandoffStrategy, ManualStrategy>();
builder.Services.AddScoped<IHandoffService, HandoffService>();

builder.Services.AddScoped<IBadgeEngine, BadgeEngine>();
builder.Services.AddScoped<IProgressService, ProgressService>();
builder.Services.AddScoped<IDashboardService, DashboardService>(); // design 2 dashboard
builder.Services.AddScoped<Indlela.Services.Roadmaps.IRoadmapService, Indlela.Services.Roadmaps.RoadmapService>(); // step 2: roadmaps
builder.Services.AddScoped<Indlela.Services.Career.ICareerService, Indlela.Services.Career.CareerService>(); // Smart Start, portfolio, job matching, forecast

// ---------- Course ingestion: import from a link + daily link check (UC-08/09/10/11 fast path) ----------
builder.Services.AddHttpClient(CrawlPolicy.ClientName, http =>
{
    http.Timeout = TimeSpan.FromSeconds(15);
    // Identify the bot honestly, as UC-08 requires.
    http.DefaultRequestHeaders.UserAgent.ParseAdd($"{CrawlPolicy.BotName}/1.0 (+https://github.com/Nandi288)");
    http.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml;q=0.9,*/*;q=0.5");
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    AutomaticDecompression = System.Net.DecompressionMethods.All,
    MaxAutomaticRedirections = 5
});
builder.Services.AddScoped<CrawlPolicy>();
builder.Services.AddScoped<ICourseImportService, CourseImportService>();
builder.Services.AddScoped<ILinkChecker, LinkChecker>();
builder.Services.AddHostedService<LinkCheckHostedService>();

builder.Services.Configure<NewsOptions>(config.GetSection(NewsOptions.Section));
builder.Services.AddHttpClient<INewsClient, NewsApiClient>((sp, http) =>
{
    var baseUrl = config[$"{NewsOptions.Section}:BaseUrl"] ?? "https://newsapi.org/v2/";
    http.BaseAddress = new Uri(baseUrl);
    http.Timeout = TimeSpan.FromSeconds(10);
    http.DefaultRequestHeaders.UserAgent.ParseAdd("Indlela/1.0 (+https://github.com/Nandi288)");
});
builder.Services.AddScoped<ITechPulseService, TechPulseService>();
builder.Services.AddHostedService<TechPulseRefreshHostedService>();

builder.Services.AddSingleton<IAppEmailSender, LoggingEmailSender>(); // swap for SendGrid / ACS

// ---------- Hangfire (when you add the packages in Indlela.csproj) ----------
// builder.Services.AddHangfire(c => c.UseSqlServerStorage(config.GetConnectionString("IndlelaDb")));
// builder.Services.AddHangfireServer();
// Then register: handoff reminders (UC-16), nudges at 08:00 SAST (UC-20), digest (UC-21), scrape (UC-09).

// ---------- Data-light delivery ----------
builder.Services.AddResponseCompression(o =>
{
    o.EnableForHttps = true;
    o.Providers.Add<BrotliCompressionProvider>();
    o.Providers.Add<GzipCompressionProvider>();
});

builder.Services.AddControllersWithViews(o =>
    o.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute()));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseResponseCompression();
// The Android emulator can't trust the local development certificate, so in Development the mobile API
// is also allowed over http (http://10.0.2.2:<port>). Everything else, and all of Production, stays https-only.
app.UseWhen(ctx => !(app.Environment.IsDevelopment() && ctx.Request.Path.StartsWithSegments("/api")),
    branch => branch.UseHttpsRedirection());
app.UseStaticFiles(new StaticFileOptions
{
    // Files linked with asp-append-version (?v=hash) are cached for a year; everything else for a day.
    OnPrepareResponse = ctx => ctx.Context.Response.Headers.CacheControl =
        ctx.Context.Request.Query.ContainsKey("v") ? "public,max-age=31536000,immutable" : "public,max-age=86400"
});

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

var demoMode = Indlela.Services.Demo.DemoMode.IsOn(app.Configuration, app.Environment);
await SeedData.InitialiseAsync(app.Services, seedDemoContent: demoMode);
await RoadmapSeed.ApplyAsync(app.Services); // 3 starter roadmaps + Indlela Academy lessons (always, only adds what's missing)
if (demoMode)
    await Indlela.Services.Demo.DemoSetup.ApplyAsync(app.Services); // simulated provider + demo news for the walkthrough

app.Run();
