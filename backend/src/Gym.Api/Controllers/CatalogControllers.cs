using Gym.Api.Auth;
using Gym.Api.Middleware;
using Gym.Application.Abstractions;
using Gym.Application.Common;
using Gym.Application.Contracts;
using Gym.Application.Features.Amenities;
using Gym.Application.Features.Chains;
using Gym.Application.Features.Gyms;
using Gym.Application.Features.Reviews;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Net.Http.Headers;

namespace Gym.Api.Controllers;

[ApiController]
[Route("api/v1/gyms")]
public sealed class GymsController : ControllerBase
{
    /// <summary>Public gym search: term, district, chain, minimum scores; stable pagination.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<GymListItemDto>>> Search(
        [FromServices] IQueryHandler<SearchGymsQuery, PagedResult<GymListItemDto>> handler,
        [FromQuery] string? term,
        [FromQuery] int? district,
        [FromQuery] string? chain,
        [FromQuery] double? minTotalScore,
        [FromQuery] double? minMembershipScore,
        [FromQuery] double? minStudioScore,
        [FromQuery] string? sort,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new SearchGymsQuery(term, district, chain, minTotalScore, minMembershipScore, minStudioScore, sort, page, pageSize),
            cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>Public gym detail including chain, amenities, opening hours and score summary.</summary>
    [HttpGet("{slug}")]
    public async Task<ActionResult<GymDetailDto>> GetBySlug(
        [FromServices] IQueryHandler<GetGymBySlugQuery, GymDetailDto> handler,
        string slug,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new GetGymBySlugQuery(slug), cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult(this);
        }

        var etag = $"W/\"gym-{result.Value.Id:N}-{result.Value.UpdatedAtUtc.UtcTicks}-{result.Value.Score.ReviewCount}\"";
        if (Request.Headers.IfNoneMatch.ToString().Contains(etag, StringComparison.Ordinal))
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        Response.Headers[HeaderNames.ETag] = etag;
        Response.Headers[HeaderNames.CacheControl] = "public, max-age=60";
        return Ok(result.Value);
    }

    /// <summary>Score summary: total, both areas, all categories, counts and scoreBasis.</summary>
    [HttpGet("{slug}/summary")]
    public async Task<ActionResult<ScoreSummaryDto>> GetSummary(
        [FromServices] IQueryHandler<GetGymSummaryQuery, ScoreSummaryDto> handler,
        string slug,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new GetGymSummaryQuery(slug), cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>Published reviews of a gym (never anonymous; includes the Google verification badge).</summary>
    [HttpGet("{slug}/reviews")]
    public async Task<ActionResult<PagedResult<ReviewDto>>> GetReviews(
        [FromServices] IQueryHandler<ListGymReviewsQuery, PagedResult<ReviewDto>> handler,
        string slug,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new ListGymReviewsQuery(slug, page, pageSize), cancellationToken);
        return result.ToActionResult(this);
    }

    public sealed record CreateReviewRequest(RatingsDto Ratings, string? Text);

    /// <summary>Creates a review (verified Google account required, at least one 1-5 rating).</summary>
    [HttpPost("{slug}/reviews")]
    [Authorize]
    [EnableRateLimiting("public-write")]
    public async Task<ActionResult<OwnReviewDto>> CreateReview(
        [FromServices] ICommandHandler<CreateReviewCommand, OwnReviewDto> handler,
        string slug,
        [FromBody] CreateReviewRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await handler.Handle(new CreateReviewCommand(userId.Value, slug, request.Ratings, request.Text), cancellationToken);
        return result.ToCreatedResult(this);
    }
}

[ApiController]
[Route("api/v1/chains")]
public sealed class ChainsController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ChainDto>>> List(
        [FromServices] IQueryHandler<ListChainsQuery, IReadOnlyList<ChainDto>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new ListChainsQuery(), cancellationToken);
        return result.ToActionResult(this);
    }
}

[ApiController]
[Route("api/v1/amenities")]
public sealed class AmenitiesController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AmenityDto>>> List(
        [FromServices] IQueryHandler<ListAmenitiesQuery, IReadOnlyList<AmenityDto>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new ListAmenitiesQuery(), cancellationToken);
        return result.ToActionResult(this);
    }
}
