using System.Net;
using System.Text.Json;
using CodeReviewAgent.Api.Models;
using CodeReviewAgent.Api.Options;
using Microsoft.Extensions.Options;

namespace CodeReviewAgent.Api.Services;

public interface IGitHubPullRequestService
{
    Task<PullRequestContext> GetPullRequestAsync(
        PullRequestRef reference,
        int? maxFiles,
        CancellationToken cancellationToken);
}

public sealed class GitHubApiException(HttpStatusCode statusCode, string message)
    : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}

public sealed class GitHubPullRequestService(
    HttpClient httpClient,
    IOptions<GitHubOptions> options,
    ILogger<GitHubPullRequestService> logger) : IGitHubPullRequestService
{
    private readonly GitHubOptions _options = options.Value;

    public async Task<PullRequestContext> GetPullRequestAsync(
        PullRequestRef reference,
        int? maxFiles,
        CancellationToken cancellationToken)
    {
        var prPath = $"repos/{reference.Owner}/{reference.Repo}/pulls/{reference.Number}";
        using var prDocument = await GetJsonAsync(prPath, reference, cancellationToken);
        var pr = prDocument.RootElement;

        var limit = Math.Clamp(maxFiles ?? _options.MaxFiles, 1, 100);
        using var filesDocument = await GetJsonAsync(
            $"{prPath}/files?per_page={limit}",
            reference,
            cancellationToken);

        var files = filesDocument.RootElement
            .EnumerateArray()
            .Select(MapFile)
            .ToList();

        logger.LogInformation(
            "Fetched {FileCount} changed files for {Slug}", files.Count, reference.Slug);

        return new PullRequestContext(
            reference,
            GetString(pr, "title") ?? $"Pull request {reference.Number}",
            GetString(pr, "body"),
            pr.TryGetProperty("user", out var user) ? GetString(user, "login") ?? "unknown" : "unknown",
            pr.TryGetProperty("base", out var b) ? GetString(b, "ref") ?? "unknown" : "unknown",
            pr.TryGetProperty("head", out var h) ? GetString(h, "ref") ?? "unknown" : "unknown",
            GetString(pr, "state") ?? "unknown",
            GetInt(pr, "additions"),
            GetInt(pr, "deletions"),
            GetInt(pr, "changed_files"),
            files);
    }

    private ChangedFile MapFile(JsonElement element)
    {
        var patch = GetString(element, "patch");
        if (patch is { Length: > 0 } && patch.Length > _options.MaxPatchCharsPerFile)
        {
            patch = patch[.._options.MaxPatchCharsPerFile] + "\n... (patch truncated)";
        }

        return new ChangedFile(
            GetString(element, "filename") ?? "unknown",
            GetString(element, "status") ?? "modified",
            GetInt(element, "additions"),
            GetInt(element, "deletions"),
            patch);
    }

    private async Task<JsonDocument> GetJsonAsync(
        string path,
        PullRequestRef reference,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(path, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var message = response.StatusCode switch
            {
                HttpStatusCode.NotFound =>
                    $"Pull request {reference.Slug} was not found, or the repository is private. " +
                    "Configure GitHub:Token to review private repositories.",
                HttpStatusCode.Forbidden =>
                    "GitHub rate limit reached. Anonymous access allows 60 requests/hour; " +
                    "configure GitHub:Token to raise the limit.",
                HttpStatusCode.Unauthorized =>
                    "GitHub rejected the configured token.",
                _ => $"GitHub request failed with status {(int)response.StatusCode}."
            };
            throw new GitHubApiException(response.StatusCode, message);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int GetInt(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : 0;
}
