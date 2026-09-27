namespace CodeReviewAgent.Api.Models;

public sealed record PullRequestRef(string Owner, string Repo, int Number)
{
    public string Slug => $"{Owner}/{Repo}#{Number}";
}

public sealed record ChangedFile(
    string Path,
    string Status,
    int Additions,
    int Deletions,
    string? Patch);

public sealed record PullRequestContext(
    PullRequestRef Reference,
    string Title,
    string? Body,
    string Author,
    string BaseBranch,
    string HeadBranch,
    string State,
    int Additions,
    int Deletions,
    int ChangedFileCount,
    IReadOnlyList<ChangedFile> Files);
