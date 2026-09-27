using System.Text.RegularExpressions;
using CodeReviewAgent.Api.Models;

namespace CodeReviewAgent.Api.Services;

public static partial class PullRequestUrlParser
{
    [GeneratedRegex(
        @"^https?://(?:www\.)?github\.com/(?<owner>[A-Za-z0-9._-]+)/(?<repo>[A-Za-z0-9._-]+)/pull/(?<number>\d+)(?:[/#?].*)?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UrlPattern();

    [GeneratedRegex(
        @"^(?<owner>[A-Za-z0-9._-]+)/(?<repo>[A-Za-z0-9._-]+)#(?<number>\d+)$",
        RegexOptions.CultureInvariant)]
    private static partial Regex SlugPattern();

    public static bool TryParse(string? input, out PullRequestRef reference)
    {
        reference = default!;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var value = input.Trim();
        var match = UrlPattern().Match(value);
        if (!match.Success)
        {
            match = SlugPattern().Match(value);
        }

        if (!match.Success || !int.TryParse(match.Groups["number"].Value, out var number) || number <= 0)
        {
            return false;
        }

        reference = new PullRequestRef(
            match.Groups["owner"].Value,
            match.Groups["repo"].Value,
            number);
        return true;
    }
}
