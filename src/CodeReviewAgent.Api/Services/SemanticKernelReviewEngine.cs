using CodeReviewAgent.Api.Models;
using CodeReviewAgent.Api.Options;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace CodeReviewAgent.Api.Services;

/// <summary>
/// Semantic Kernel orchestrated review against Azure OpenAI, with the GitHub
/// repository plugin registered as a callable tool layer.
/// </summary>
public sealed class SemanticKernelReviewEngine(
    IOptions<AzureOpenAIOptions> options,
    ILogger<SemanticKernelReviewEngine> logger) : IReviewEngine
{
    private readonly AzureOpenAIOptions _options = options.Value;

    public string Name => $"semantic-kernel:{_options.DeploymentName}";

    public async Task<ReviewResult> ReviewAsync(
        string pullRequestUrl,
        PullRequestContext context,
        CancellationToken cancellationToken)
    {
        var builder = Kernel.CreateBuilder();
        builder.AddAzureOpenAIChatCompletion(
            deploymentName: _options.DeploymentName,
            endpoint: _options.Endpoint!,
            apiKey: _options.ApiKey!);

        var kernel = builder.Build();
        kernel.Plugins.AddFromObject(new GitHubRepositoryPlugin(context), "github");

        var chat = kernel.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory(ReviewPrompt.SystemInstructions);
        history.AddUserMessage(ReviewPrompt.BuildUserPrompt(context));

        var settings = new OpenAIPromptExecutionSettings
        {
            Temperature = 0.1,
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
            ResponseFormat = "json_object"
        };

        var response = await chat.GetChatMessageContentAsync(
            history, settings, kernel, cancellationToken);

        var raw = response.Content ?? string.Empty;
        logger.LogDebug("Model returned {Length} characters for {Slug}", raw.Length, context.Reference.Slug);

        var payload = ReviewPayloadMapper.Parse(raw);
        return ReviewPayloadMapper.ToResult(payload, pullRequestUrl, context, Name);
    }
}
