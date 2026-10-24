using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Indlela.Migrations
{
    /// <inheritdoc />
    public partial class AddCareerFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Surname = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SouthAfricanIdOrPassport = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    TargetSaqaNqfLevel = table.Column<int>(type: "int", nullable: true),
                    CareerGoal = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Province = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IsUnder18 = table.Column<bool>(type: "bit", nullable: false),
                    Consent_TermsAcceptedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Consent_PrivacyNoticeAcceptedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Consent_AutofillSharingAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Consent_AutofillSharingWithdrawnAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Consent_MarketingEmailAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Consent_MarketingEmailWithdrawnAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Badges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IconUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsVerifiedOnly = table.Column<bool>(type: "bit", nullable: false),
                    Criteria = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RuleType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RequiredCount = table.Column<int>(type: "int", nullable: false),
                    RuleCategory = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Badges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Courses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(220)", maxLength: 220, nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    OriginalUrl = table.Column<string>(type: "nvarchar(850)", maxLength: 850, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    NqfLevel = table.Column<int>(type: "int", nullable: true),
                    Credits = table.Column<int>(type: "int", nullable: true),
                    AccreditationBody = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    IsAccredited = table.Column<bool>(type: "bit", nullable: false),
                    DeliveryMode = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    LicenceType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Language = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    IsFree = table.Column<bool>(type: "bit", nullable: false),
                    EstimatedHours = table.Column<int>(type: "int", nullable: true),
                    ThumbnailUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    LastVerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsBeingReverified = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Courses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JobRoles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Slug = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Field = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    OpeningTypes = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LearningPaths",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Slug = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Stream = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Careers = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningPaths", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProviderFormMappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProviderName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Strategy = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    RegistrationUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    FieldMapJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AllowsTrackingParameter = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    NeedsReview = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderFormMappings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SearchEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Query = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Filters = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ResultCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClickedCourseId = table.Column<int>(type: "int", nullable: true),
                    ClickedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SearchEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LearnerProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Interest = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Education = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Experience = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    HoursPerWeek = table.Column<int>(type: "int", nullable: false),
                    Connectivity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Goal = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RecommendedPathSlug = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearnerProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LearnerProfiles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PortfolioProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Headline = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    About = table.Column<string>(type: "nvarchar(800)", maxLength: 800, nullable: true),
                    InformalExperience = table.Column<string>(type: "nvarchar(800)", maxLength: 800, nullable: true),
                    Location = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ShowEmail = table.Column<bool>(type: "bit", nullable: false),
                    ShowPhone = table.Column<bool>(type: "bit", nullable: false),
                    IsPublic = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortfolioProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PortfolioProfiles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BadgeAwards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BadgeId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DateAwarded = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsSelfReportedEvidence = table.Column<bool>(type: "bit", nullable: false),
                    IsHidden = table.Column<bool>(type: "bit", nullable: false),
                    VerificationCode = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BadgeAwards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BadgeAwards_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BadgeAwards_Badges_BadgeId",
                        column: x => x.BadgeId,
                        principalTable: "Badges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LearnerEnrollments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CourseId = table.Column<int>(type: "int", nullable: false),
                    HandoffStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    HandoffStrategyUsed = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    HandoffStartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EnrolledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProblemReason = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ProblemDetails = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CurrentPercentage = table.Column<int>(type: "int", nullable: false),
                    LastProgressSource = table.Column<string>(type: "nvarchar(14)", maxLength: 14, nullable: true),
                    LastActivityAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletionSource = table.Column<string>(type: "nvarchar(14)", maxLength: 14, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearnerEnrollments", x => x.Id);
                    table.CheckConstraint("CK_Enrollment_Percentage", "[CurrentPercentage] BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "FK_LearnerEnrollments_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LearnerEnrollments_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TechPulseArticles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: true),
                    SourceUrl = table.Column<string>(type: "nvarchar(850)", maxLength: 850, nullable: false),
                    SourceName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CachedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AlignedCourseId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechPulseArticles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TechPulseArticles_Courses_AlignedCourseId",
                        column: x => x.AlignedCourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "JobRoleSkills",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JobRoleId = table.Column<int>(type: "int", nullable: false),
                    Skill = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobRoleSkills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobRoleSkills_JobRoles_JobRoleId",
                        column: x => x.JobRoleId,
                        principalTable: "JobRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LearnerPaths",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LearningPathId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearnerPaths", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LearnerPaths_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LearnerPaths_LearningPaths_LearningPathId",
                        column: x => x.LearningPathId,
                        principalTable: "LearningPaths",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RoadmapSteps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LearningPathId = table.Column<int>(type: "int", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Stage = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    WhatYouLearn = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CourseId = table.Column<int>(type: "int", nullable: true),
                    ExternalUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ProviderName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    CostNote = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    EstimatedHours = table.Column<int>(type: "int", nullable: true),
                    Skills = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ProofType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoadmapSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoadmapSteps_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_RoadmapSteps_LearningPaths_LearningPathId",
                        column: x => x.LearningPathId,
                        principalTable: "LearningPaths",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HandoffEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EnrollmentId = table.Column<int>(type: "int", nullable: false),
                    Strategy = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    MappingVersion = table.Column<int>(type: "int", nullable: true),
                    SharedFields = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HandoffEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HandoffEvents_LearnerEnrollments_EnrollmentId",
                        column: x => x.EnrollmentId,
                        principalTable: "LearnerEnrollments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProgressEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EnrollmentId = table.Column<int>(type: "int", nullable: false),
                    ModuleOrLessonName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Percentage = table.Column<int>(type: "int", nullable: false),
                    ProgressSource = table.Column<string>(type: "nvarchar(14)", maxLength: 14, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgressEntries", x => x.Id);
                    table.CheckConstraint("CK_Progress_Percentage", "[Percentage] BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "FK_ProgressEntries_LearnerEnrollments_EnrollmentId",
                        column: x => x.EnrollmentId,
                        principalTable: "LearnerEnrollments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LearnerStepProgress",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RoadmapStepId = table.Column<int>(type: "int", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EvidenceUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearnerStepProgress", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LearnerStepProgress_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LearnerStepProgress_RoadmapSteps_RoadmapStepId",
                        column: x => x.RoadmapStepId,
                        principalTable: "RoadmapSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BadgeAwards_BadgeId",
                table: "BadgeAwards",
                column: "BadgeId");

            migrationBuilder.CreateIndex(
                name: "IX_BadgeAwards_UserId_BadgeId",
                table: "BadgeAwards",
                columns: new[] { "UserId", "BadgeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BadgeAwards_VerificationCode",
                table: "BadgeAwards",
                column: "VerificationCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Badges_Name",
                table: "Badges",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Courses_Category_NqfLevel",
                table: "Courses",
                columns: new[] { "Category", "NqfLevel" });

            migrationBuilder.CreateIndex(
                name: "IX_Courses_IsPublished_LastVerifiedAt",
                table: "Courses",
                columns: new[] { "IsPublished", "LastVerifiedAt" })
                .Annotation("SqlServer:Include", new[] { "Title", "Provider", "Category", "NqfLevel", "DeliveryMode" });

            migrationBuilder.CreateIndex(
                name: "IX_Courses_NqfLevel",
                table: "Courses",
                column: "NqfLevel");

            migrationBuilder.CreateIndex(
                name: "IX_Courses_OriginalUrl",
                table: "Courses",
                column: "OriginalUrl");

            migrationBuilder.CreateIndex(
                name: "IX_Courses_Provider",
                table: "Courses",
                column: "Provider");

            migrationBuilder.CreateIndex(
                name: "IX_Courses_Slug",
                table: "Courses",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HandoffEvents_EnrollmentId",
                table: "HandoffEvents",
                column: "EnrollmentId");

            migrationBuilder.CreateIndex(
                name: "IX_HandoffEvents_OccurredAt",
                table: "HandoffEvents",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_JobRoles_Slug",
                table: "JobRoles",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobRoleSkills_JobRoleId",
                table: "JobRoleSkills",
                column: "JobRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_LearnerEnrollments_CourseId",
                table: "LearnerEnrollments",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_LearnerEnrollments_HandoffStatus_HandoffStartedAt",
                table: "LearnerEnrollments",
                columns: new[] { "HandoffStatus", "HandoffStartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LearnerEnrollments_UserId_CourseId",
                table: "LearnerEnrollments",
                columns: new[] { "UserId", "CourseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LearnerPaths_ActivePerUser",
                table: "LearnerPaths",
                column: "UserId",
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_LearnerPaths_LearningPathId",
                table: "LearnerPaths",
                column: "LearningPathId");

            migrationBuilder.CreateIndex(
                name: "IX_LearnerPaths_UserId_LearningPathId",
                table: "LearnerPaths",
                columns: new[] { "UserId", "LearningPathId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LearnerProfiles_UserId",
                table: "LearnerProfiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LearnerStepProgress_RoadmapStepId",
                table: "LearnerStepProgress",
                column: "RoadmapStepId");

            migrationBuilder.CreateIndex(
                name: "IX_LearnerStepProgress_UserId_RoadmapStepId",
                table: "LearnerStepProgress",
                columns: new[] { "UserId", "RoadmapStepId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LearningPaths_Slug",
                table: "LearningPaths",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioProfiles_Slug",
                table: "PortfolioProfiles",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioProfiles_UserId",
                table: "PortfolioProfiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProgressEntries_EnrollmentId_Timestamp",
                table: "ProgressEntries",
                columns: new[] { "EnrollmentId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderFormMappings_ActivePerProvider",
                table: "ProviderFormMappings",
                column: "ProviderName",
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderFormMappings_ProviderName_Version",
                table: "ProviderFormMappings",
                columns: new[] { "ProviderName", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoadmapSteps_CourseId",
                table: "RoadmapSteps",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_RoadmapSteps_LearningPathId_Order",
                table: "RoadmapSteps",
                columns: new[] { "LearningPathId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SearchEvents_CreatedAt",
                table: "SearchEvents",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TechPulseArticles_AlignedCourseId",
                table: "TechPulseArticles",
                column: "AlignedCourseId");

            migrationBuilder.CreateIndex(
                name: "IX_TechPulseArticles_Category_PublishedAt",
                table: "TechPulseArticles",
                columns: new[] { "Category", "PublishedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TechPulseArticles_SourceUrl",
                table: "TechPulseArticles",
                column: "SourceUrl",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "BadgeAwards");

            migrationBuilder.DropTable(
                name: "HandoffEvents");

            migrationBuilder.DropTable(
                name: "JobRoleSkills");

            migrationBuilder.DropTable(
                name: "LearnerPaths");

            migrationBuilder.DropTable(
                name: "LearnerProfiles");

            migrationBuilder.DropTable(
                name: "LearnerStepProgress");

            migrationBuilder.DropTable(
                name: "PortfolioProfiles");

            migrationBuilder.DropTable(
                name: "ProgressEntries");

            migrationBuilder.DropTable(
                name: "ProviderFormMappings");

            migrationBuilder.DropTable(
                name: "SearchEvents");

            migrationBuilder.DropTable(
                name: "TechPulseArticles");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "Badges");

            migrationBuilder.DropTable(
                name: "JobRoles");

            migrationBuilder.DropTable(
                name: "RoadmapSteps");

            migrationBuilder.DropTable(
                name: "LearnerEnrollments");

            migrationBuilder.DropTable(
                name: "LearningPaths");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "Courses");
        }
    }
}
