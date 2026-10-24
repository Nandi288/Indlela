using Indlela.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Indlela.Data;

public class IndlelaDbContext : IdentityDbContext<ApplicationUser>
{
    private readonly IDataProtector _idProtector;

    public IndlelaDbContext(DbContextOptions<IndlelaDbContext> options, IDataProtectionProvider dataProtection)
        : base(options)
    {
        _idProtector = dataProtection.CreateProtector("Indlela.PersonalData.IdOrPassport.v1");
    }

    public DbSet<Course> Courses => Set<Course>();
    public DbSet<LearnerEnrollment> LearnerEnrollments => Set<LearnerEnrollment>();
    public DbSet<ProgressEntry> ProgressEntries => Set<ProgressEntry>();
    public DbSet<Badge> Badges => Set<Badge>();
    public DbSet<BadgeAward> BadgeAwards => Set<BadgeAward>();
    public DbSet<TechPulseArticle> TechPulseArticles => Set<TechPulseArticle>();
    public DbSet<ProviderFormMapping> ProviderFormMappings => Set<ProviderFormMapping>();
    public DbSet<HandoffEvent> HandoffEvents => Set<HandoffEvent>();
    public DbSet<SearchEvent> SearchEvents => Set<SearchEvent>();

    // Roadmaps (work-readiness paths)
    public DbSet<LearningPath> LearningPaths => Set<LearningPath>();
    public DbSet<RoadmapStep> RoadmapSteps => Set<RoadmapStep>();
    public DbSet<LearnerPath> LearnerPaths => Set<LearnerPath>();
    public DbSet<LearnerStepProgress> LearnerStepProgress => Set<LearnerStepProgress>();

    // Smart Start, Skills Portfolio and Job Matching
    public DbSet<LearnerProfile> LearnerProfiles => Set<LearnerProfile>();
    public DbSet<PortfolioProfile> PortfolioProfiles => Set<PortfolioProfile>();
    public DbSet<JobRole> JobRoles => Set<JobRole>();
    public DbSet<JobRoleSkill> JobRoleSkills => Set<JobRoleSkill>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder); // Identity tables first

        // ---------- ApplicationUser (UC-01, UC-03) ----------
        builder.Entity<ApplicationUser>(u =>
        {
            // Encrypted at rest. Ciphertext is not deterministic, so this column can't be searched or indexed — by design.
            // EF caches the model, so capture the protector in a local (not `this`) to avoid holding the first context alive.
            var protector = _idProtector;
            var idConverter = new ValueConverter<string?, string?>(
                plain => plain == null ? null : protector.Protect(plain),
                cipher => cipher == null ? null : protector.Unprotect(cipher));

            u.Property(x => x.SouthAfricanIdOrPassport)
                .HasConversion(idConverter)
                .HasMaxLength(512);

            u.Property(x => x.Province).HasConversion<string>().HasMaxLength(20);

            u.OwnsOne(x => x.ConsentTimestamps, c =>
            {
                c.Property(p => p.TermsAcceptedAt).HasColumnName("Consent_TermsAcceptedAt");
                c.Property(p => p.PrivacyNoticeAcceptedAt).HasColumnName("Consent_PrivacyNoticeAcceptedAt");
                c.Property(p => p.AutofillSharingConsentAt).HasColumnName("Consent_AutofillSharingAt");
                c.Property(p => p.AutofillSharingWithdrawnAt).HasColumnName("Consent_AutofillSharingWithdrawnAt");
                c.Property(p => p.MarketingEmailConsentAt).HasColumnName("Consent_MarketingEmailAt");
                c.Property(p => p.MarketingEmailWithdrawnAt).HasColumnName("Consent_MarketingEmailWithdrawnAt");
            });
            u.Navigation(x => x.ConsentTimestamps).IsRequired();

            u.Ignore(x => x.MaskedIdOrPassport);
        });

        // ---------- Course (UC-04, UC-05, UC-07, UC-11) ----------
        builder.Entity<Course>(c =>
        {
            c.HasIndex(x => x.Slug).IsUnique();

            // The catalogue query: published + recently verified, newest first.
            c.HasIndex(x => new { x.IsPublished, x.LastVerifiedAt })
                .IncludeProperties(x => new { x.Title, x.Provider, x.Category, x.NqfLevel, x.DeliveryMode });

            // Filter columns (UC-05).
            c.HasIndex(x => new { x.Category, x.NqfLevel });
            c.HasIndex(x => x.Provider);
            c.HasIndex(x => x.NqfLevel);

            // De-duplication in the ingestion pipeline (UC-10).
            c.HasIndex(x => x.OriginalUrl);

            c.Property(x => x.DeliveryMode).HasConversion<string>().HasMaxLength(12);
            c.Property(x => x.OriginalUrl).HasMaxLength(850); // nvarchar(850) = 1,700 bytes, the SQL Server index key limit
        });

        // ---------- LearnerEnrollment (UC-13, UC-16, UC-18) ----------
        builder.Entity<LearnerEnrollment>(e =>
        {
            e.HasOne(x => x.User)
                .WithMany(u => u.Enrollments)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade); // account deletion removes enrollments (UC-03 alt 4b)

            e.HasOne(x => x.Course)
                .WithMany(c => c.Enrollments)
                .HasForeignKey(x => x.CourseId)
                .OnDelete(DeleteBehavior.Restrict); // never lose learner history by deleting a course — unpublish instead

            // A learner has at most one enrollment per course.
            e.HasIndex(x => new { x.UserId, x.CourseId }).IsUnique();

            // Conversion KPI + reminder jobs.
            e.HasIndex(x => new { x.HandoffStatus, x.HandoffStartedAt });

            e.Property(x => x.HandoffStatus).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.HandoffStrategyUsed).HasConversion<string>().HasMaxLength(12);
            e.Property(x => x.ProblemReason).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.LastProgressSource).HasConversion<string>().HasMaxLength(14);
            e.Property(x => x.CompletionSource).HasConversion<string>().HasMaxLength(14);

            e.ToTable(t => t.HasCheckConstraint("CK_Enrollment_Percentage", "[CurrentPercentage] BETWEEN 0 AND 100"));
        });

        // ---------- ProgressEntry (UC-17, UC-30) ----------
        builder.Entity<ProgressEntry>(p =>
        {
            p.HasOne(x => x.Enrollment)
                .WithMany(e => e.ProgressEntries)
                .HasForeignKey(x => x.EnrollmentId)
                .OnDelete(DeleteBehavior.Cascade);

            p.HasIndex(x => new { x.EnrollmentId, x.Timestamp });
            p.Property(x => x.ProgressSource).HasConversion<string>().HasMaxLength(14);
            p.ToTable(t => t.HasCheckConstraint("CK_Progress_Percentage", "[Percentage] BETWEEN 0 AND 100"));
        });

        // ---------- Badge & BadgeAward (UC-31, UC-32) ----------
        builder.Entity<Badge>(b =>
        {
            b.HasIndex(x => x.Name).IsUnique();
            b.Property(x => x.RuleType).HasConversion<string>().HasMaxLength(20);
        });

        builder.Entity<BadgeAward>(a =>
        {
            a.HasOne(x => x.Badge)
                .WithMany(b => b.Awards)
                .HasForeignKey(x => x.BadgeId)
                .OnDelete(DeleteBehavior.Restrict); // issued badges stay valid even if a rule changes (UC-32 alt 4a)

            a.HasOne(x => x.User)
                .WithMany(u => u.BadgeAwards)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            a.HasIndex(x => new { x.UserId, x.BadgeId }).IsUnique(); // each badge once per learner
            a.HasIndex(x => x.VerificationCode).IsUnique();
        });

        // ---------- TechPulseArticle (UC-22, UC-23, UC-24) ----------
        builder.Entity<TechPulseArticle>(t =>
        {
            t.HasOne(x => x.AlignedCourse)
                .WithMany()
                .HasForeignKey(x => x.AlignedCourseId)
                .OnDelete(DeleteBehavior.SetNull); // removing a course just drops the "Learn this skill" chip

            t.Property(x => x.SourceUrl).HasMaxLength(850);
            t.HasIndex(x => x.SourceUrl).IsUnique(); // de-duplication on refresh
            t.HasIndex(x => new { x.Category, x.PublishedAt });
        });

        // ---------- Handoff plumbing (UC-12, UC-13) ----------
        builder.Entity<ProviderFormMapping>(m =>
        {
            m.Property(x => x.Strategy).HasConversion<string>().HasMaxLength(12);
            m.HasIndex(x => new { x.ProviderName, x.Version }).IsUnique();
            // One active mapping per provider.
            m.HasIndex(x => x.ProviderName).IsUnique().HasFilter("[IsActive] = 1")
                .HasDatabaseName("IX_ProviderFormMappings_ActivePerProvider");
        });

        builder.Entity<HandoffEvent>(h =>
        {
            h.HasOne(x => x.Enrollment)
                .WithMany()
                .HasForeignKey(x => x.EnrollmentId)
                .OnDelete(DeleteBehavior.Cascade);
            h.Property(x => x.Strategy).HasConversion<string>().HasMaxLength(12);
            h.HasIndex(x => x.OccurredAt);
        });

        builder.Entity<SearchEvent>(s =>
        {
            s.HasIndex(x => x.CreatedAt);
        });

        // ---------- Roadmaps ----------
        builder.Entity<LearningPath>(p =>
        {
            p.HasIndex(x => x.Slug).IsUnique();
            p.HasMany(x => x.Steps).WithOne(x => x.LearningPath!)
                .HasForeignKey(x => x.LearningPathId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<RoadmapStep>(r =>
        {
            r.HasIndex(x => new { x.LearningPathId, x.Order }).IsUnique();
            r.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            r.HasOne(x => x.Course).WithMany()
                .HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<LearnerPath>(l =>
        {
            l.HasIndex(x => new { x.UserId, x.LearningPathId }).IsUnique();
            // One active roadmap per learner.
            l.HasIndex(x => x.UserId).IsUnique().HasFilter("[IsActive] = 1")
                .HasDatabaseName("IX_LearnerPaths_ActivePerUser");
            l.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            l.HasOne(x => x.LearningPath).WithMany().HasForeignKey(x => x.LearningPathId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<LearnerStepProgress>(l =>
        {
            l.HasIndex(x => new { x.UserId, x.RoadmapStepId }).IsUnique();
            l.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            l.HasOne(x => x.RoadmapStep).WithMany().HasForeignKey(x => x.RoadmapStepId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<LearnerProfile>(l =>
        {
            l.HasIndex(x => x.UserId).IsUnique();
            l.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<PortfolioProfile>(p =>
        {
            p.HasIndex(x => x.UserId).IsUnique();
            p.HasIndex(x => x.Slug).IsUnique();
            p.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<JobRole>(j =>
        {
            j.HasIndex(x => x.Slug).IsUnique();
            j.HasMany(x => x.Skills).WithOne(x => x.JobRole!).HasForeignKey(x => x.JobRoleId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
