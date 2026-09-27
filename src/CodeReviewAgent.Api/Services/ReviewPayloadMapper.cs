using System.Text.Json;
using System.Text.Json.Serialization;
using CodeReviewAgent.Api.Models;

namespace CodeReviewAgent.Api.Services;

internal sealed class LlmReviewPayload
{
    public string? Summary { get; set; }
    public string? Recommendation { get; set; }
    public int OverallConfidence { get; set; }
    public List<LlmCategoryScore>? CategoryScores { get; set; }
    public List<LlmFinding>? Findings { get; set; }
}

internal sealed class LlmCategoryScore
{
    public string? Category { get; set; }
    public int Score { get; set; }
    public string? Summary { get; set; }
}

internal sealed class LlmFinding
{
    public string? File { get; set; }
    public int? Line { get; set; }
    public string? Category { get; set; }
    public string? Severity { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? RecommendedFix { get; set; }
    public string? SuggestedCode { get; set; }
    public int Confidence { get; set; }
}

internal static class ReviewPayloadMapper
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public static LlmReviewPayload Parse(string raw)
    {
        var json = ExtractJson(raw);
        var payload = JsonSerializer.Deserialize<LlmReviewPayload>(json, Options)
            ?? throw new InvalidOperationException("Model returned an empty review payload.");
        return payload;
    }

    public static ReviewResult ToResult(
        LlmReviewPayload payload,
        string pullRequestUrl,
        PullRequestContext context,
        string engine)
    {
        var findings = (payload.Findings ?? [])
            .Select(f => new ReviewFinding
            {
                File = string.IsNullOrWhiteSpace(f.File) ? "(unspecified)" : f.File,
                Line = f.Line,
                Category = ParseEnum(f.Category, ReviewCategory.Quality),
                Severity = ParseEnum(f.Severity, Severity.Medium),
                Title = string.IsNullOrWhiteSpace(f.Title) ? "Untitled finding" : f.Title,
                Description = f.Description ?? string.Empty,
                RecommendedFix = f.RecommendedFix,
                SuggestedCode = f.SuggestedCode,
                Confidence = Math.Clamp(f.Confidence, 0, 100)
            })
            .OrderByDescending(f => f.Severity)
            .ThenByDescending(f => f.Confidence)
            .ToList();

        var scores = (payload.CategoryScores ?? [])
            .Select(s => new CategoryScore(
                ParseEnum(s.Category, ReviewCategory.Quality),
                Math.Clamp(s.Score, 0, 100),
                s.Summary ?? string.Empty))
            .ToList();

        return new ReviewResult
        {
            ReviewId = Guid.NewGuid().ToString("n"),
            PullRequestUrl = pullRequestUrl,
            PullRequest = context,
            Summary = payload.Summary ?? "No summary produced.",
            OverallConfidence = Math.Clamp(payload.OverallConfidence, 0, 100),
            Recommendation = NormalizeRecommendation(payload.Recommendation, findings),
            CategoryScores = scores,
            Findings = findings,
            Engine = engine
        };
    }

    private static string NormalizeRecommendation(string? value, IReadOnlyList<ReviewFinding> findings)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value.Trim().ToLowerInvariant().Replace(' ', '_');
        }

        return findings.Any(f => f.Severity >= Severity.High) ? "request_changes"
            : findings.Count > 0 ? "comment"
            : "approve";
    }

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback) where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ? parsed : fallback;

    private static string ExtractJson(string raw)
    {
        var text = raw.Trim();
        if (text.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewLine = text.IndexOf('\n');
            if (firstNewLine >= 0)
            {
                text = text[(firstNewLine + 1)..];
            }

            var fenceEnd = text.LastIndexOf("```", StringComparison.Ordinal);
            if (fenceEnd >= 0)
            {
                text = text[..fenceEnd];
            }

            text = text.Trim();
        }

        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            throw new InvalidOperationException("Model response did not contain a JSON object.");
        }

        return text[start..(end + 1)];
    }
}
