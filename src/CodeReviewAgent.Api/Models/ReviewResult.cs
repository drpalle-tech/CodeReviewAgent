using System.Text.Json.Serialization;

namespace CodeReviewAgent.Api.Models;

[JsonConverter(typeof(JsonStringEnumConverter<Severity>))]
public enum Severity
{
    Info,
    Low,
    Medium,
    High,
    Critical
}

[JsonConverter(typeof(JsonStringEnumConverter<ReviewCategory>))]
public enum ReviewCategory
{
    Quality,
    Security,
    Testing,
    Maintainability,
    Performance
}

public sealed record ReviewFinding
{
    public string Id { get; init; } = Guid.NewGuid().ToString("n")[..8];
    public required string File { get; init; }
    public int? Line { get; init; }
    public required ReviewCategory Category { get; init; }
    public required Severity Severity { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public string? RecommendedFix { get; init; }
    public string? SuggestedCode { get; init; }

    /// <summary>Model confidence that the finding is real, 0-100.</summary>
    public int Confidence { get; init; }
}

public sealed record CategoryScore(ReviewCategory Category, int Score, string Summary);

public sealed record ReviewResult
{
    public required string ReviewId { get; init; }
    public required string PullRequestUrl { get; init; }
    public required PullRequestContext PullRequest { get; init; }
    public required string Summary { get; init; }

    /// <summary>Overall confidence in the review, 0-100.</summary>
    public required int OverallConfidence { get; init; }

    public required string Recommendation { get; init; }
    public required IReadOnlyList<CategoryScore> CategoryScores { get; init; }
    public required IReadOnlyList<ReviewFinding> Findings { get; init; }
    public required string Engine { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public IReadOnlyDictionary<string, int> SeverityCounts =>
        Findings.GroupBy(f => f.Severity.ToString())
                .ToDictionary(g => g.Key, g => g.Count());
}

public sealed record ReviewRequest
{
    public required string PullRequestUrl { get; init; }
    public int? MaxFiles { get; init; }
}
