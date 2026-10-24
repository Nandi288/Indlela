using System.ComponentModel.DataAnnotations;

namespace Indlela.Models;

/// <summary>A career roadmap a learner can follow, e.g. "Digital Marketing". Made of ordered <see cref="RoadmapStep"/>s.</summary>
public class LearningPath
{
    public int Id { get; set; }

    [Required, MaxLength(80)]
    public string Slug { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Tech, Design, Business or Finance — used to group paths.</summary>
    [Required, MaxLength(30)]
    public string Stream { get; set; } = "Tech";

    [Required, MaxLength(400)]
    public string Summary { get; set; } = string.Empty;

    /// <summary>Entry-level roles this path leads to, separated by "|".</summary>
    [MaxLength(300)]
    public string Careers { get; set; } = string.Empty;

    public int SortOrder { get; set; }
    public bool IsPublished { get; set; } = true;

    public ICollection<RoadmapStep> Steps { get; set; } = new List<RoadmapStep>();
}

public enum RoadmapStepType
{
    /// <summary>A free Indlela lesson + quiz (an in-app <see cref="Course"/>). Done when the course is completed.</summary>
    IndlelaLesson = 0,
    /// <summary>A free course on another platform. The learner marks it done (step 3 adds the certificate link).</summary>
    ExternalCourse = 1,
    /// <summary>A small portfolio project that proves the skills of the whole path.</summary>
    Capstone = 2
}

/// <summary>One thing to learn on a roadmap: what to know, where to learn it free, and what proves it.</summary>
public class RoadmapStep
{
    public int Id { get; set; }

    public int LearningPathId { get; set; }
    public LearningPath? LearningPath { get; set; }

    /// <summary>Order within the path (1, 2, 3…).</summary>
    public int Order { get; set; }

    /// <summary>Stage heading, e.g. "Foundations", "Build skills", "Prove it".</summary>
    [Required, MaxLength(60)]
    public string Stage { get; set; } = string.Empty;

    [Required, MaxLength(160)]
    public string Title { get; set; } = string.Empty;

    /// <summary>What the learner will know after this step.</summary>
    [Required, MaxLength(600)]
    public string WhatYouLearn { get; set; } = string.Empty;

    public RoadmapStepType Type { get; set; }

    /// <summary>For <see cref="RoadmapStepType.IndlelaLesson"/>: the in-app course to take.</summary>
    public int? CourseId { get; set; }
    public Course? Course { get; set; }

    /// <summary>For external courses: where to learn it, always the provider's own page.</summary>
    [MaxLength(500)]
    public string? ExternalUrl { get; set; }

    [MaxLength(120)]
    public string? ProviderName { get; set; }

    /// <summary>Honest cost note shown to learners, e.g. "Free" or "Free to audit; certificate may cost".</summary>
    [MaxLength(160)]
    public string CostNote { get; set; } = "Free";

    public int? EstimatedHours { get; set; }

    /// <summary>Skills this step proves, separated by "|". These feed the learner's portfolio.</summary>
    [MaxLength(400)]
    public string Skills { get; set; } = string.Empty;

    /// <summary>What counts as proof, e.g. "Indlela quiz", "Certificate link", "Project link".</summary>
    [MaxLength(80)]
    public string ProofType { get; set; } = string.Empty;
}

/// <summary>The roadmap a learner is following. One active path at a time; switching keeps all progress.</summary>
public class LearnerPath
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public int LearningPathId { get; set; }
    public LearningPath? LearningPath { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}

/// <summary>Learner-reported completion of an external course or capstone step. Indlela lessons use the enrollment instead.</summary>
public class LearnerStepProgress
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public int RoadmapStepId { get; set; }
    public RoadmapStep? RoadmapStep { get; set; }

    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Optional link the learner adds now (certificate or project). Step 3 adds verification.</summary>
    [MaxLength(500)]
    public string? EvidenceUrl { get; set; }
}
