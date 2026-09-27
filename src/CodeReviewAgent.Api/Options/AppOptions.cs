namespace CodeReviewAgent.Api.Options;

public sealed class GitHubOptions
{
    public const string SectionName = "GitHub";

    public string ApiBaseUrl { get; set; } = "https://api.github.com/";

    /// <summary>Optional PAT. Anonymous access works for public repos but is limited to 60 requests/hour.</summary>
    public string? Token { get; set; }

    public int MaxFiles { get; set; } = 30;

    public int MaxPatchCharsPerFile { get; set; } = 8000;
}

public sealed class AzureOpenAIOptions
{
    public const string SectionName = "AzureOpenAI";

    public string? Endpoint { get; set; }
    public string? ApiKey { get; set; }
    public string DeploymentName { get; set; } = "gpt-4o";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Endpoint) && !string.IsNullOrWhiteSpace(ApiKey);
}
