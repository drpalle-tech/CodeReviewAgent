using System.Net.Http.Headers;
using CodeReviewAgent.Api.Options;
using CodeReviewAgent.Api.Services;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.Configure<GitHubOptions>(builder.Configuration.GetSection(GitHubOptions.SectionName));
builder.Services.Configure<AzureOpenAIOptions>(builder.Configuration.GetSection(AzureOpenAIOptions.SectionName));

builder.Services.AddHttpClient<IGitHubPullRequestService, GitHubPullRequestService>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<GitHubOptions>>().Value;
    client.BaseAddress = new Uri(options.ApiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("CodeReviewAgent", "1.0"));
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");

    if (!string.IsNullOrWhiteSpace(options.Token))
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.Token);
    }
});

builder.Services.AddSingleton<HeuristicReviewEngine>();
builder.Services.AddSingleton<SemanticKernelReviewEngine>();
builder.Services.AddScoped<ICodeReviewService, CodeReviewService>();

const string CorsPolicy = "web";
builder.Services.AddCors(cors => cors.AddPolicy(CorsPolicy, policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                 ?? ["http://localhost:5173"])
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors(CorsPolicy);
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();

public partial class Program;
