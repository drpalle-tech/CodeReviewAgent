using CodeReviewAgent.Api.Models;
using CodeReviewAgent.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace CodeReviewAgent.Api.Controllers;

[ApiController]
[Route("api/reviews")]
public sealed class ReviewsController(ICodeReviewService reviewService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<ReviewResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] ReviewRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await reviewService.ReviewAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest, title: "Invalid pull request URL");
        }
        catch (GitHubApiException ex)
        {
            return Problem(ex.Message, statusCode: (int)ex.StatusCode, title: "GitHub request failed");
        }
    }

    [HttpGet("{reviewId}")]
    [ProducesResponseType<ReviewResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetById(string reviewId)
    {
        var result = reviewService.Get(reviewId);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet]
    public IActionResult GetRecent([FromQuery] int take = 10) => Ok(reviewService.GetRecent(take));
}
