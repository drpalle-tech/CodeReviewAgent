using System.Collections.Concurrent;
using CodeReviewAgent.Api.Models;
using CodeReviewAgent.Api.Options;
using Microsoft.Extensions.Options;

namespace CodeReviewAgent.Api.Services;

public interface ICodeReviewService
{
    Task<ReviewResult> ReviewAsync(ReviewRequest request, CancellationToken cancellationToken);
    ReviewResult? Get(string reviewId);
    IReadOnlyList<ReviewResult> GetRecent(int take);
}

public sealed class CodeReviewService(
    IGitHubPullRequestService gitHub,
    SemanticKernelReviewEngine semanticKernelEngine,
    HeuristicReviewEngine heuristicEngine,
    IOptions<AzureOpenAIOptions> azureOptions,
    ILogger<CodeReviewService> logger) : ICodeReviewService
{
    private static readonly ConcurrentDictionary<string, ReviewResult> Store = new();
    private static readonly ConcurrentQueue<string> Order = new();
    private const int MaxStored = 50;

    public async Task<ReviewResult> ReviewAsync(ReviewRequest request, CancellationToken cancellationToken)
    {
        if (!PullRequestUrlParser.TryParse(request.PullRequestUrl, out var reference))
        {
            throw new ArgumentException(
                "Expected a GitHub pull request URL such as https://github.com/owner/repo/pull/123.",
                nameof(request));
        }

        var context = await gitHub.GetPullRequestAsync(reference, request.MaxFiles, cancellationToken);
        var result = await RunEngineAsync(request.PullRequestUrl, context, cancellationToken);

        Store[result.ReviewId] = result;
        Order.Enqueue(result.ReviewId);
        while (Order.Count > MaxStored && Order.TryDequeue(out var evicted))
        {
            Store.TryRemove(evicted, out _);
        }

        return result;
    }

    private async Task<ReviewResult> RunEngineAsync(
        string url,
        PullRequestContext context,
        CancellationToken cancellationToken)
    {
        if (!azureOptions.Value.IsConfigured)
        {
            logger.LogWarning("Azure OpenAI is not configured; using the heuristic engine.");
            return await heuristicEngine.ReviewAsync(url, context, cancellationToken);
        }

        try
        {
            return await semanticKernelEngine.ReviewAsync(url, context, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "LLM review failed for {Slug}; falling back to heuristics.", context.Reference.Slug);
            return await heuristicEngine.ReviewAsync(url, context, cancellationToken);
        }
    }

    public ReviewResult? Get(string reviewId) =>
        Store.TryGetValue(reviewId, out var result) ? result : null;

    public IReadOnlyList<ReviewResult> GetRecent(int take) =>
        Store.Values
            .OrderByDescending(r => r.CreatedAt)
            .Take(Math.Clamp(take, 1, MaxStored))
            .ToList();
}
