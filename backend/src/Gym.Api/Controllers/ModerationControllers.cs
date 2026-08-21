using Gym.Api.Auth;
using Gym.Api.Middleware;
using Gym.Application.Abstractions;
using Gym.Application.Common;
using Gym.Application.Contracts;
using Gym.Application.Features.Contact;
using Gym.Application.Features.Reviews;
using Gym.Application.Features.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gym.Api.Controllers;

[ApiController]
[Route("api/v1/moderation")]
[Authorize(Policy = "Moderator")]
public sealed class ModerationController : ControllerBase
{
    /// <summary>Moderation queue: reviews by status (Published, SoftDeleted, UnderReview, RemovedLegal).</summary>
    [HttpGet("reviews")]
    public async Task<ActionResult<PagedResult<ModerationReviewDto>>> Queue(
        [FromServices] IQueryHandler<ModerationQueueQuery, PagedResult<ModerationReviewDto>> handler,
        [FromQuery] string status = "UnderReview",
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.Handle(new ModerationQueueQuery(status, page, pageSize), cancellationToken);
        return result.ToActionResult(this);
    }

    public sealed record RemoveReviewRequest(string Reason);

    /// <summary>Moderator removal = reversible soft delete with a documented reason.</summary>
    [HttpPost("reviews/{reviewId:guid}/remove")]
    public async Task<ActionResult> Remove(
        [FromServices] ICommandHandler<ModeratorRemoveReviewCommand> handler,
        Guid reviewId,
        [FromBody] RemoveReviewRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await handler.Handle(
            new ModeratorRemoveReviewCommand(userId.Value, User.GetRole(), reviewId, request.Reason),
            cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>Restores a soft deleted review (Admin only).</summary>
    [HttpPost("reviews/{reviewId:guid}/restore")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult> Restore(
        [FromServices] ICommandHandler<RestoreReviewCommand> handler,
        Guid reviewId,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new RestoreReviewCommand(reviewId), cancellationToken);
        return result.ToActionResult(this);
    }
}

[ApiController]
[Route("api/v1/admin/users")]
[Authorize(Policy = "Admin")]
public sealed class AdminUsersController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<UserAdminDto>>> List(
        [FromServices] IQueryHandler<ListUsersQuery, PagedResult<UserAdminDto>> handler,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new ListUsersQuery(page, pageSize), cancellationToken);
        return result.ToActionResult(this);
    }

    public sealed record SetRoleRequest(string Role);

    [HttpPut("{userId:guid}/role")]
    public async Task<ActionResult> SetRole(
        [FromServices] ICommandHandler<SetUserRoleCommand> handler,
        Guid userId,
        [FromBody] SetRoleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new SetUserRoleCommand(userId, request.Role), cancellationToken);
        return result.ToActionResult(this);
    }
}

[ApiController]
[Route("api/v1/admin/contact-requests")]
[Authorize(Policy = "Admin")]
public sealed class AdminContactRequestsController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<ContactRequestDto>>> List(
        [FromServices] IQueryHandler<ListContactRequestsQuery, PagedResult<ContactRequestDto>> handler,
        [FromQuery] string? status,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new ListContactRequestsQuery(status, page, pageSize), cancellationToken);
        return result.ToActionResult(this);
    }

    public sealed record StatusRequest(string Status);

    [HttpPut("{requestId:guid}/status")]
    public async Task<ActionResult> SetStatus(
        [FromServices] ICommandHandler<SetContactRequestStatusCommand> handler,
        Guid requestId,
        [FromBody] StatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new SetContactRequestStatusCommand(requestId, request.Status), cancellationToken);
        return result.ToActionResult(this);
    }
}
