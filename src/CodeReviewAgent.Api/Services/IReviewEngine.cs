using CodeReviewAgent.Api.Models;

namespace CodeReviewAgent.Api.Services;

public interface IReviewEngine
{
    string Name { get; }

    Task<ReviewResult> ReviewAsync(
        string pullRequestUrl,
        PullRequestContext context,
        CancellationToken cancellationToken);
}
