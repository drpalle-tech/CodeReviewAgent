using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CodeReviewAgent.Api.Tests;

public class ReviewsApiTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Health_endpoint_reports_healthy()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.EnsureSuccessStatusCode();
        Assert.Contains("healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Invalid_pull_request_url_returns_bad_request()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/reviews", new { pullRequestUrl = "not-a-url" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_review_id_returns_not_found()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/reviews/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
