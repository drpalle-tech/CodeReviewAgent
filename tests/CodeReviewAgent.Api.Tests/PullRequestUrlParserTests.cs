using CodeReviewAgent.Api.Services;

namespace CodeReviewAgent.Api.Tests;

public class PullRequestUrlParserTests
{
    [Theory]
    [InlineData("https://github.com/dotnet/runtime/pull/12345", "dotnet", "runtime", 12345)]
    [InlineData("http://www.github.com/octocat/Hello-World/pull/7/files", "octocat", "Hello-World", 7)]
    [InlineData("https://github.com/owner/repo.name/pull/99#issuecomment-1", "owner", "repo.name", 99)]
    [InlineData("dotnet/runtime#42", "dotnet", "runtime", 42)]
    public void Parses_valid_references(string input, string owner, string repo, int number)
    {
        Assert.True(PullRequestUrlParser.TryParse(input, out var reference));
        Assert.Equal(owner, reference.Owner);
        Assert.Equal(repo, reference.Repo);
        Assert.Equal(number, reference.Number);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("https://github.com/dotnet/runtime/issues/12345")]
    [InlineData("https://gitlab.com/dotnet/runtime/pull/1")]
    [InlineData("https://github.com/dotnet/runtime/pull/abc")]
    [InlineData("https://github.com/dotnet/runtime/pull/0")]
    public void Rejects_invalid_references(string? input)
    {
        Assert.False(PullRequestUrlParser.TryParse(input, out _));
    }

    [Fact]
    public void Slug_is_formatted_for_display()
    {
        Assert.True(PullRequestUrlParser.TryParse("https://github.com/a/b/pull/3", out var reference));
        Assert.Equal("a/b#3", reference.Slug);
    }
}
