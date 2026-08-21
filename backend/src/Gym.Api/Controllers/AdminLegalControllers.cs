using System.Text.Json;
using Gym.Api.Auth;
using Gym.Api.Middleware;
using Gym.Application.Abstractions;
using Gym.Application.Common;
using Gym.Application.Contracts;
using Gym.Application.Features.Legal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gym.Api.Controllers;

[ApiController]
[Route("api/v1/admin/legal-cases")]
public sealed class AdminLegalCasesController : ControllerBase
{
    /// <summary>Case list for staff (Moderator and Admin).</summary>
    [HttpGet]
    [Authorize(Policy = "Moderator")]
    public async Task<ActionResult<PagedResult<LegalCaseListItemDto>>> List(
        [FromServices] IQueryHandler<ListCasesQuery, PagedResult<LegalCaseListItemDto>> handler,
        [FromQuery] string? status,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new ListCasesQuery(status, page, pageSize), cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>Full case detail including the append-only audit event trail and appeals.</summary>
    [HttpGet("{caseId:guid}")]
    [Authorize(Policy = "Moderator")]
    public async Task<ActionResult<LegalCaseDetailDto>> Detail(
        [FromServices] IQueryHandler<GetCaseDetailQuery, LegalCaseDetailDto> handler,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new GetCaseDetailQuery(caseId), cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>Case export as a downloadable JSON document.</summary>
    [HttpGet("{caseId:guid}/export")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult> Export(
        [FromServices] IQueryHandler<GetCaseDetailQuery, LegalCaseDetailDto> handler,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new GetCaseDetailQuery(caseId), cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult(this);
        }

        var json = JsonSerializer.SerializeToUtf8Bytes(result.Value, new JsonSerializerOptions { WriteIndented = true });
        return File(json, "application/json", $"{result.Value.CaseNumber}.json");
    }

    public sealed record ClassifyRequest(string Classification);

    /// <summary>Classifies a case (Normal or FastTrackObviouslyIllegal; fast-track hides the review).</summary>
    [HttpPost("{caseId:guid}/classify")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult> Classify(
        [FromServices] ICommandHandler<ClassifyCaseCommand> handler,
        Guid caseId,
        [FromBody] ClassifyRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await handler.Handle(new ClassifyCaseCommand(userId.Value, caseId, request.Classification), cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost("{caseId:guid}/start-review")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult> StartReview(
        [FromServices] ICommandHandler<StartCaseReviewCommand> handler,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await handler.Handle(new StartCaseReviewCommand(userId.Value, caseId), cancellationToken);
        return result.ToActionResult(this);
    }

    public sealed record DecideRequest(string Decision, string Rationale);

    /// <summary>Documented decision: KeepOnline or FullyRemoved (review becomes RemovedLegal).</summary>
    [HttpPost("{caseId:guid}/decide")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult> Decide(
        [FromServices] ICommandHandler<DecideCaseCommand> handler,
        Guid caseId,
        [FromBody] DecideRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await handler.Handle(new DecideCaseCommand(userId.Value, caseId, request.Decision, request.Rationale), cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost("{caseId:guid}/close")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult> Close(
        [FromServices] ICommandHandler<CloseCaseCommand> handler,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await handler.Handle(new CloseCaseCommand(userId.Value, caseId), cancellationToken);
        return result.ToActionResult(this);
    }

    public sealed record DecideAppealRequest(string Outcome, string Rationale);

    [HttpPost("appeals/{appealId:guid}/decide")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult> DecideAppeal(
        [FromServices] ICommandHandler<DecideAppealCommand> handler,
        Guid appealId,
        [FromBody] DecideAppealRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await handler.Handle(new DecideAppealCommand(userId.Value, appealId, request.Outcome, request.Rationale), cancellationToken);
        return result.ToActionResult(this);
    }
}

[ApiController]
[Route("api/v1/admin/legal-holds")]
[Authorize(Policy = "Admin")]
public sealed class AdminLegalHoldsController : ControllerBase
{
    public sealed record HoldRequest(string Reason, Guid? LegalCaseId, Guid? ReviewId, Guid? UserId);

    /// <summary>Applies a legal hold; retention deletion is paused while a hold is active.</summary>
    [HttpPost]
    public async Task<ActionResult> Apply(
        [FromServices] ICommandHandler<ApplyLegalHoldCommand, Guid> handler,
        [FromBody] HoldRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await handler.Handle(
            new ApplyLegalHoldCommand(userId.Value, request.Reason, request.LegalCaseId, request.ReviewId, request.UserId),
            cancellationToken);
        return result.Map(id => new { id }).ToCreatedResult(this);
    }

    [HttpPost("{holdId:guid}/release")]
    public async Task<ActionResult> Release(
        [FromServices] ICommandHandler<ReleaseLegalHoldCommand> handler,
        Guid holdId,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await handler.Handle(new ReleaseLegalHoldCommand(userId.Value, holdId), cancellationToken);
        return result.ToActionResult(this);
    }
}

[ApiController]
[Route("api/v1/admin/legal-documents")]
[Authorize(Policy = "Admin")]
public sealed class AdminLegalDocumentsController : ControllerBase
{
    public sealed record CreateVersionRequest(string Type, string Title, string ContentMarkdown);

    /// <summary>Creates a new draft version of a legal document.</summary>
    [HttpPost]
    public async Task<ActionResult> CreateVersion(
        [FromServices] ICommandHandler<CreateLegalDocumentVersionCommand, Guid> handler,
        [FromBody] CreateVersionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new CreateLegalDocumentVersionCommand(request.Type, request.Title, request.ContentMarkdown),
            cancellationToken);
        return result.Map(id => new { id }).ToCreatedResult(this);
    }

    [HttpPost("{documentId:guid}/publish")]
    public async Task<ActionResult> Publish(
        [FromServices] ICommandHandler<PublishLegalDocumentCommand> handler,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new PublishLegalDocumentCommand(documentId), cancellationToken);
        return result.ToActionResult(this);
    }
}
