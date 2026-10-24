using Indlela.Models;

namespace Indlela.ViewModels;

public enum StepStatus { ToDo = 0, Next = 1, InProgress = 2, Done = 3 }

/// <summary>One roadmap on the /paths list.</summary>
public class PathCardVm
{
    public string Slug { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Stream { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public IReadOnlyList<string> Careers { get; init; } = Array.Empty<string>();
    public int StepCount { get; init; }
    public int TotalHours { get; init; }
    public int DoneCount { get; init; }
    public bool IsActive { get; init; }
    public bool HasStarted { get; init; }
    public int Percent => StepCount == 0 ? 0 : DoneCount * 100 / StepCount;
}

public class PathIndexVm
{
    public IReadOnlyList<PathCardVm> Paths { get; init; } = Array.Empty<PathCardVm>();
    public bool IsSignedIn { get; init; }
    public PathCardVm? Active => Paths.FirstOrDefault(p => p.IsActive);
}

/// <summary>One step on a roadmap, with this learner's status.</summary>
public class RoadmapStepVm
{
    public int Id { get; init; }
    public int Order { get; init; }
    public string Stage { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string WhatYouLearn { get; init; } = string.Empty;
    public RoadmapStepType Type { get; init; }
    public int? CourseId { get; init; }
    public string? ExternalUrl { get; init; }
    public string? ProviderName { get; init; }
    public string CostNote { get; init; } = "Free";
    public int? EstimatedHours { get; init; }
    public IReadOnlyList<string> Skills { get; init; } = Array.Empty<string>();
    public string ProofType { get; init; } = string.Empty;

    public StepStatus Status { get; set; }
    /// <summary>In-app lessons only: course progress from the enrollment.</summary>
    public int Percent { get; init; }
    public bool IsEnrolled { get; init; }
    public string? EvidenceUrl { get; init; }
    public DateTime? CompletedAt { get; init; }

    public bool IsDone => Status == StepStatus.Done;
}

public class PathDetailsVm
{
    public int Id { get; init; }
    public string Slug { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Stream { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public IReadOnlyList<string> Careers { get; init; } = Array.Empty<string>();
    public IReadOnlyList<RoadmapStepVm> Steps { get; init; } = Array.Empty<RoadmapStepVm>();

    public bool IsSignedIn { get; init; }
    /// <summary>This is the learner's active roadmap.</summary>
    public bool IsActive { get; init; }
    /// <summary>The learner follows a different roadmap right now.</summary>
    public string? OtherActiveName { get; init; }
    public DateTime? CompletedAt { get; init; }

    public int DoneCount => Steps.Count(s => s.IsDone);
    public int Percent => Steps.Count == 0 ? 0 : DoneCount * 100 / Steps.Count;
    public int TotalHours => Steps.Sum(s => s.EstimatedHours ?? 0);
    public RoadmapStepVm? NextStep => Steps.FirstOrDefault(s => !s.IsDone);
}
