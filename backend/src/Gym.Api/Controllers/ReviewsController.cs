using Gym.Api.Auth;
using Gym.Api.Middleware;
using Gym.Application.Abstractions;
using Gym.Application.Contracts;
using Gym.Application.Features.Legal;
using Gym.Application.Features.Reviews;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Gym.Api.Controllers;

[ApiController]
[Route("api/v1/reviews")]
public sealed class ReviewsController : ControllerBase
{
    public sealed record UpdateReviewRequest(RatingsDto Ratings, string? Text);

    /// <summary>Updates the caller's own review; the previous state is archived as a revision.</summary>
    [HttpPut("{reviewId:guid}")]
    [Authorize]
    [EnableRateLimiting("public-write")]
    public async Task<ActionResult<OwnReviewDto>> UpdateOwn(
        [FromServices] ICommandHandler<UpdateOwnReviewCommand, OwnReviewDto> handler,
        Guid reviewId,
        [FromBody] UpdateReviewRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await handler.Handle(new UpdateOwnReviewCommand(userId.Value, reviewId, request.Ratings, request.Text), cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>Reversibly soft deletes the caller's own review.</summary>
    [HttpDelete("{reviewId:guid}")]
    [Authorize]
    public async Task<ActionResult> DeleteOwn(
        [FromServices] ICommandHandler<DeleteOwnReviewCommand> handler,
        Guid reviewId,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await handler.Handle(new DeleteOwnReviewCommand(userId.Value, reviewId), cancellationToken);
        return result.ToActionResult(this);
    }

    public sealed record ReportReviewRequest(
        string Category,
        string ReporterName,
        string ReporterEmail,
        string Description,
        string? Website);

    /// <summary>
    /// Public review report; creates a LegalCase and returns the case number plus a one-time status token.
    /// The "website" field is a honeypot and must stay empty.
    /// </summary>
    [HttpPost("{reviewId:guid}/report")]
    [EnableRateLimiting("public-write")]
    public async Task<ActionResult<ReportReviewResultDto>> Report(
        [FromServices] ICommandHandler<ReportReviewCommand, ReportReviewResultDto> handler,
        Guid reviewId,
        [FromBody] ReportReviewRequest request,
        CancellationToken cancellationToken)
    {
        // Honeypot: bots fill hidden fields; drop silently with a generic acknowledgement.
        if (!string.IsNullOrEmpty(request.Website))
        {
            return Accepted(new { status = "received" });
        }

        var result = await handler.Handle(
            new ReportReviewCommand(reviewId, request.Category, request.ReporterName, request.ReporterEmail, request.Description),
            cancellationToken);
        return result.ToCreatedResult(this);
    }
}
