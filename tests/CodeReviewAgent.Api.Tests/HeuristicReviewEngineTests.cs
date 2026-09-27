using CodeReviewAgent.Api.Models;
using CodeReviewAgent.Api.Services;

namespace CodeReviewAgent.Api.Tests;

public class HeuristicReviewEngineTests
{
    private static PullRequestContext Context(params ChangedFile[] files) => new(
        new PullRequestRef("owner", "repo", 1),
        "Test PR",
        "body",
        "octocat",
        "main",
        "feature",
        "open",
        files.Sum(f => f.Additions),
        files.Sum(f => f.Deletions),
        files.Length,
        files);

    private static ChangedFile File(string path, string addedLine, int additions = 1) => new(
        path,
        "modified",
        additions,
        0,
        $"@@ -1,2 +1,3 @@\n context\n+{addedLine}\n");

    [Fact]
    public async Task Flags_hard_coded_secret_as_critical()
    {
        var engine = new HeuristicReviewEngine();
        var context = Context(File("src/Config.cs", "var apiKey = \"sk-live-abc123456\";"));

        var result = await engine.ReviewAsync("url", context, CancellationToken.None);

        var finding = Assert.Single(result.Findings, f => f.Category == ReviewCategory.Security);
        Assert.Equal(Severity.Critical, finding.Severity);
        Assert.Equal("request_changes", result.Recommendation);
        Assert.False(string.IsNullOrWhiteSpace(finding.RecommendedFix));
        Assert.InRange(finding.Confidence, 1, 100);
    }

    [Fact]
    public async Task Flags_missing_tests_when_only_source_changed()
    {
        var engine = new HeuristicReviewEngine();
        var context = Context(File("src/Service.cs", "var x = 1;"));

        var result = await engine.ReviewAsync("url", context, CancellationToken.None);

        Assert.Contains(result.Findings, f => f.Category == ReviewCategory.Testing);
    }

    [Fact]
    public async Task Does_not_flag_missing_tests_when_tests_changed()
    {
        var engine = new HeuristicReviewEngine();
        var context = Context(
            File("src/Service.cs", "var x = 1;"),
            File("tests/ServiceTests.cs", "Assert.True(true);"));

        var result = await engine.ReviewAsync("url", context, CancellationToken.None);

        Assert.DoesNotContain(result.Findings, f => f.Category == ReviewCategory.Testing);
    }

    [Fact]
    public async Task Clean_diff_produces_approve_and_category_scores()
    {
        var engine = new HeuristicReviewEngine();
        var context = Context(new ChangedFile("docs/readme.md", "modified", 2, 0, "@@ -1 +1,2 @@\n+# Title\n"));

        var result = await engine.ReviewAsync("url", context, CancellationToken.None);

        Assert.Empty(result.Findings);
        Assert.Equal("approve", result.Recommendation);
        Assert.Equal(Enum.GetValues<ReviewCategory>().Length, result.CategoryScores.Count);
        Assert.All(result.CategoryScores, s => Assert.InRange(s.Score, 0, 100));
    }

    [Fact]
    public async Task Findings_are_ordered_by_severity()
    {
        var engine = new HeuristicReviewEngine();
        var context = Context(
            new ChangedFile("src/A.cs", "modified", 3, 0,
                "@@ -1,1 +1,4 @@\n+// TODO: clean up\n+var password = \"hunter2abc\";\n+Console.WriteLine(1);\n"));

        var result = await engine.ReviewAsync("url", context, CancellationToken.None);

        Assert.True(result.Findings.Count >= 3);
        Assert.Equal(Severity.Critical, result.Findings[0].Severity);
        Assert.True(result.SeverityCounts["Critical"] >= 1);
    }
}
