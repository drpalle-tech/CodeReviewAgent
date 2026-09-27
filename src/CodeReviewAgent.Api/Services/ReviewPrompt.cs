using System.Text;
using CodeReviewAgent.Api.Models;

namespace CodeReviewAgent.Api.Services;

public static class ReviewPrompt
{
    public const string SystemInstructions = """
        You are a senior staff engineer performing a rigorous pull request review.
        Assess four dimensions: code quality, security, test coverage, and maintainability.

        Rules:
        - Only report issues you can justify from the supplied diff.
        - Prefer a few high-signal findings over many trivial ones.
        - Every finding must include a concrete recommended fix.
        - Confidence values are integers from 0 to 100.
        - Respond with raw JSON only. No markdown fences, no commentary.

        JSON schema:
        {
          "summary": string,
          "recommendation": "approve" | "comment" | "request_changes",
          "overallConfidence": int,
          "categoryScores": [
            { "category": "Quality"|"Security"|"Testing"|"Maintainability"|"Performance",
              "score": int, "summary": string }
          ],
          "findings": [
            { "file": string, "line": int|null,
              "category": "Quality"|"Security"|"Testing"|"Maintainability"|"Performance",
              "severity": "Info"|"Low"|"Medium"|"High"|"Critical",
              "title": string, "description": string,
              "recommendedFix": string, "suggestedCode": string|null,
              "confidence": int }
          ]
        }
        """;

    public static string BuildUserPrompt(PullRequestContext context)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Repository: {context.Reference.Owner}/{context.Reference.Repo}");
        builder.AppendLine($"Pull request #{context.Reference.Number}: {context.Title}");
        builder.AppendLine($"Author: {context.Author}");
        builder.AppendLine($"Branches: {context.HeadBranch} -> {context.BaseBranch}");
        builder.AppendLine(
            $"Change size: +{context.Additions}/-{context.Deletions} across {context.ChangedFileCount} files");

        if (!string.IsNullOrWhiteSpace(context.Body))
        {
            builder.AppendLine();
            builder.AppendLine("Description:");
            builder.AppendLine(context.Body.Length > 2000 ? context.Body[..2000] : context.Body);
        }

        builder.AppendLine();
        builder.AppendLine("Changed files and diffs:");

        foreach (var file in context.Files)
        {
            builder.AppendLine();
            builder.AppendLine($"### {file.Path} ({file.Status}, +{file.Additions}/-{file.Deletions})");
            builder.AppendLine(string.IsNullOrWhiteSpace(file.Patch)
                ? "(no textual diff available - binary or too large)"
                : file.Patch);
        }

        builder.AppendLine();
        builder.AppendLine("Return the review as JSON matching the schema.");
        return builder.ToString();
    }
}
