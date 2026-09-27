using System.Text.RegularExpressions;
using CodeReviewAgent.Api.Models;

namespace CodeReviewAgent.Api.Services;

/// <summary>
/// Deterministic rule-based reviewer used when no LLM provider is configured.
/// Keeps the product usable offline and provides a baseline for LLM output.
/// </summary>
public sealed partial class HeuristicReviewEngine : IReviewEngine
{
    public string Name => "heuristic-rules";

    private sealed record Rule(
        string Pattern,
        ReviewCategory Category,
        Severity Severity,
        string Title,
        string Description,
        string Fix,
        int Confidence);

    private static readonly Rule[] Rules =
    [
        new(@"(?i)(password|secret|api[_-]?key|token)\s*[:=]\s*""[^""]{6,}""",
            ReviewCategory.Security, Severity.Critical,
            "Possible hard-coded credential",
            "An added line assigns a literal value to a credential-like identifier.",
            "Move the value to configuration or a secret store and inject it at runtime.", 75),
        new(@"(?i)\b(select|insert|update|delete)\b.*\+\s*\w+",
            ReviewCategory.Security, Severity.High,
            "Possible SQL built via string concatenation",
            "SQL text appears to be concatenated with a variable, which enables injection.",
            "Use parameterized queries or an ORM instead of string concatenation.", 60),
        new(@"(?i)\b(eval|exec)\s*\(",
            ReviewCategory.Security, Severity.High,
            "Dynamic code execution",
            "Dynamic evaluation of code or commands was introduced.",
            "Replace dynamic execution with an explicit allow-listed dispatch.", 65),
        new(@"(?i)catch\s*(\(.*\))?\s*\{\s*\}",
            ReviewCategory.Quality, Severity.Medium,
            "Empty catch block",
            "An exception is swallowed without logging or handling.",
            "Log the exception or rethrow so failures are observable.", 70),
        new(@"(?i)//\s*(todo|fixme|hack)\b|#\s*(todo|fixme)\b",
            ReviewCategory.Maintainability, Severity.Low,
            "Unresolved TODO/FIXME introduced",
            "The change adds a TODO/FIXME marker.",
            "Resolve the item or link it to a tracked issue.", 85),
        new(@"(?i)console\.log\(|System\.out\.println\(|Console\.WriteLine\(",
            ReviewCategory.Quality, Severity.Low,
            "Ad-hoc console logging",
            "Direct console output was added instead of structured logging.",
            "Use the project logger so output is leveled and routable.", 65),
        new(@"(?i)\bany\b\s*[;,)\]]|:\s*any\b",
            ReviewCategory.Maintainability, Severity.Low,
            "TypeScript 'any' usage",
            "Use of 'any' removes type safety from the change.",
            "Introduce an explicit interface or generic type.", 50),
        new(@"(?i)verify\s*=\s*False|ServerCertificateCustomValidationCallback|rejectUnauthorized:\s*false",
            ReviewCategory.Security, Severity.High,
            "TLS verification disabled",
            "Certificate validation appears to be disabled.",
            "Restore certificate validation and trust the proper CA instead.", 80)
    ];

    /// <summary>Matches a path segment that is exactly test/tests/spec/specs, e.g. tests/, __tests__/, foo.spec.ts.</summary>
    [GeneratedRegex(@"(?i)(^|[\\/._-])(tests?|specs?)([\\/._-]|$)", RegexOptions.CultureInvariant)]
    private static partial Regex TestSegmentPattern();

    /// <summary>Matches CamelCase test file names such as ServiceTests.cs or UserSpec.ts.</summary>
    [GeneratedRegex(@"(Test|Tests|Spec|Specs)\.[A-Za-z0-9]+$", RegexOptions.CultureInvariant)]
    private static partial Regex TestSuffixPattern();

    private static bool IsTestFile(string path) =>
        TestSegmentPattern().IsMatch(path) || TestSuffixPattern().IsMatch(path);

    [GeneratedRegex(@"(?i)\.(cs|ts|tsx|js|jsx|py|java|go|rb|php|rs|cpp|c|kt)$", RegexOptions.CultureInvariant)]
    private static partial Regex CodeFilePattern();

    public Task<ReviewResult> ReviewAsync(
        string pullRequestUrl,
        PullRequestContext context,
        CancellationToken cancellationToken)
    {
        var findings = new List<ReviewFinding>();

        foreach (var file in context.Files)
        {
            findings.AddRange(ScanFile(file));

            var changed = file.Additions + file.Deletions;
            if (changed > 400)
            {
                findings.Add(new ReviewFinding
                {
                    File = file.Path,
                    Category = ReviewCategory.Maintainability,
                    Severity = Severity.Medium,
                    Title = "Very large change in a single file",
                    Description = $"{changed} lines changed in {file.Path}, which is hard to review safely.",
                    RecommendedFix = "Split the change into smaller, focused commits or pull requests.",
                    Confidence = 70
                });
            }
        }

        var codeFiles = context.Files.Where(f => CodeFilePattern().IsMatch(f.Path)).ToList();
        var testFiles = context.Files.Where(f => IsTestFile(f.Path)).ToList();

        if (codeFiles.Count > 0 && testFiles.Count == 0)
        {
            findings.Add(new ReviewFinding
            {
                File = "(pull request)",
                Category = ReviewCategory.Testing,
                Severity = Severity.High,
                Title = "No test files changed",
                Description = $"{codeFiles.Count} source files changed but no test files were added or updated.",
                RecommendedFix = "Add unit or integration tests covering the new behaviour.",
                Confidence = 80
            });
        }

        var ordered = findings
            .OrderByDescending(f => f.Severity)
            .ThenByDescending(f => f.Confidence)
            .ToList();

        var result = new ReviewResult
        {
            ReviewId = Guid.NewGuid().ToString("n"),
            PullRequestUrl = pullRequestUrl,
            PullRequest = context,
            Summary = BuildSummary(context, ordered),
            OverallConfidence = ordered.Count == 0 ? 55 : (int)ordered.Average(f => f.Confidence),
            Recommendation = ordered.Any(f => f.Severity >= Severity.High) ? "request_changes"
                : ordered.Count > 0 ? "comment"
                : "approve",
            CategoryScores = BuildScores(ordered),
            Findings = ordered,
            Engine = Name
        };

        return Task.FromResult(result);
    }

    private static IEnumerable<ReviewFinding> ScanFile(ChangedFile file)
    {
        if (string.IsNullOrWhiteSpace(file.Patch))
        {
            yield break;
        }

        var lineNumber = 0;
        foreach (var line in file.Patch.Split('\n'))
        {
            if (line.StartsWith("@@", StringComparison.Ordinal))
            {
                lineNumber = ParseHunkStart(line);
                continue;
            }

            if (!line.StartsWith('+') || line.StartsWith("+++", StringComparison.Ordinal))
            {
                if (!line.StartsWith('-'))
                {
                    lineNumber++;
                }

                continue;
            }

            var added = line[1..];
            foreach (var rule in Rules)
            {
                if (Regex.IsMatch(added, rule.Pattern, RegexOptions.None, TimeSpan.FromSeconds(1)))
                {
                    yield return new ReviewFinding
                    {
                        File = file.Path,
                        Line = lineNumber,
                        Category = rule.Category,
                        Severity = rule.Severity,
                        Title = rule.Title,
                        Description = $"{rule.Description} Line: `{added.Trim()}`",
                        RecommendedFix = rule.Fix,
                        Confidence = rule.Confidence
                    };
                }
            }

            lineNumber++;
        }
    }

    private static int ParseHunkStart(string hunkHeader)
    {
        var match = Regex.Match(hunkHeader, @"\+(\d+)", RegexOptions.None, TimeSpan.FromSeconds(1));
        return match.Success && int.TryParse(match.Groups[1].Value, out var start) ? start : 0;
    }

    private static List<CategoryScore> BuildScores(IReadOnlyList<ReviewFinding> findings)
    {
        var categories = Enum.GetValues<ReviewCategory>();
        var scores = new List<CategoryScore>(categories.Length);

        foreach (var category in categories)
        {
            var relevant = findings.Where(f => f.Category == category).ToList();
            var penalty = relevant.Sum(f => f.Severity switch
            {
                Severity.Critical => 40,
                Severity.High => 25,
                Severity.Medium => 12,
                Severity.Low => 5,
                _ => 2
            });

            scores.Add(new CategoryScore(
                category,
                Math.Clamp(100 - penalty, 0, 100),
                relevant.Count == 0
                    ? "No rule-based issues detected."
                    : $"{relevant.Count} issue(s) detected."));
        }

        return scores;
    }

    private static string BuildSummary(PullRequestContext context, IReadOnlyList<ReviewFinding> findings)
    {
        if (findings.Count == 0)
        {
            return $"Rule-based scan of {context.Files.Count} changed file(s) found no issues. " +
                   "Configure Azure OpenAI for a deeper semantic review.";
        }

        var critical = findings.Count(f => f.Severity >= Severity.High);
        return $"Rule-based scan of {context.Files.Count} changed file(s) found {findings.Count} issue(s), " +
               $"{critical} of them high severity or above. " +
               "Configure Azure OpenAI for a deeper semantic review.";
    }
}
