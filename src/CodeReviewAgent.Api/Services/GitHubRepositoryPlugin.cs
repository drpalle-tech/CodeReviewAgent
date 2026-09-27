using System.ComponentModel;
using System.Text.Json;
using CodeReviewAgent.Api.Models;
using Microsoft.SemanticKernel;

namespace CodeReviewAgent.Api.Services;

/// <summary>
/// Tool layer exposed to the LLM so it can pull repository context on demand
/// instead of relying purely on the prompt payload.
/// </summary>
public sealed class GitHubRepositoryPlugin(PullRequestContext context)
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false
    };

    [KernelFunction("get_pull_request_metadata")]
    [Description("Returns title, author, branches and change statistics for the pull request under review.")]
    public string GetPullRequestMetadata() => JsonSerializer.Serialize(new
    {
        context.Reference.Owner,
        context.Reference.Repo,
        context.Reference.Number,
        context.Title,
        context.Author,
        context.BaseBranch,
        context.HeadBranch,
        context.State,
        context.Additions,
        context.Deletions,
        context.ChangedFileCount
    }, SerializerOptions);

    [KernelFunction("list_changed_files")]
    [Description("Lists the changed file paths with status and line counts.")]
    public string ListChangedFiles() => JsonSerializer.Serialize(
        context.Files.Select(f => new { f.Path, f.Status, f.Additions, f.Deletions }),
        SerializerOptions);

    [KernelFunction("get_file_patch")]
    [Description("Returns the unified diff patch for a single changed file path.")]
    public string GetFilePatch(
        [Description("Exact file path as returned by list_changed_files")] string path)
    {
        var file = context.Files.FirstOrDefault(
            f => string.Equals(f.Path, path, StringComparison.OrdinalIgnoreCase));

        return file?.Patch ?? $"No patch available for '{path}'.";
    }
}
