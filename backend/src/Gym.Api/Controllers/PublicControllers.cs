using Gym.Api.Middleware;
using Gym.Application.Abstractions;
using Gym.Application.Contracts;
using Gym.Application.Features.Analytics;
using Gym.Application.Features.Contact;
using Gym.Application.Features.Legal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Net.Http.Headers;

namespace Gym.Api.Controllers;

[ApiController]
[Route("api/v1/legal")]
public sealed class LegalController : ControllerBase
{
    /// <summary>Active published version of a legal document (imprint, privacyPolicy, termsOfUse).</summary>
    [HttpGet("documents/{type}")]
    public async Task<ActionResult<LegalDocumentDto>> GetActiveDocument(
        [FromServices] IQueryHandler<GetActiveLegalDocumentQuery, LegalDocumentDto> handler,
        string type,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new GetActiveLegalDocumentQuery(type), cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult(this);
        }

        var etag = $"W/\"legaldoc-{result.Value.Id:N}-v{result.Value.Version}\"";
        if (Request.Headers.IfNoneMatch.ToString().Contains(etag, StringComparison.Ordinal))
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        Response.Headers[HeaderNames.ETag] = etag;
        Response.Headers[HeaderNames.CacheControl] = "public, max-age=300";
        return Ok(result.Value);
    }

    /// <summary>Version history metadata of a legal document type.</summary>
    [HttpGet("documents/{type}/versions")]
    public async Task<ActionResult<IReadOnlyList<LegalDocumentDto>>> GetDocumentVersions(
        [FromServices] IQueryHandler<ListLegalDocumentVersionsQuery, IReadOnlyList<LegalDocumentDto>> handler,
        string type,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new ListLegalDocumentVersionsQuery(type), cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>Public record of processing activities (GDPR Art. 30). Draft pending legal review.</summary>
    [HttpGet("processing-activities")]
    public ActionResult GetProcessingActivities() => Ok(new
    {
        version = ProcessingActivitiesRecord.Version,
        notice = LegalMailTexts.DraftMarker,
        activities = ProcessingActivitiesRecord.Activities,
    });

    /// <summary>Aggregated, PII-free transparency report about legal cases.</summary>
    [HttpGet("transparency-report")]
    public async Task<ActionResult<TransparencyReportDto>> GetTransparencyReport(
        [FromServices] IQueryHandler<TransparencyReportQuery, TransparencyReportDto> handler,
        [FromQuery] int? year,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new TransparencyReportQuery(year ?? DateTimeOffset.UtcNow.Year), cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>Case status for reporters, accessible only with the confidential status token.</summary>
    [HttpGet("cases/{caseNumber}/status")]
    public async Task<ActionResult<LegalCaseStatusPublicDto>> GetCaseStatus(
        [FromServices] IQueryHandler<GetCaseStatusByTokenQuery, LegalCaseStatusPublicDto> handler,
        string caseNumber,
        [FromQuery] string token,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new GetCaseStatusByTokenQuery(caseNumber, token), cancellationToken);
        return result.ToActionResult(this);
    }

    public sealed record SubmitAppealRequest(string Token, string Text, string? Website);

    /// <summary>Tokenized appeal against a case decision (open at least six months after the decision).</summary>
    [HttpPost("cases/{caseNumber}/appeal")]
    [EnableRateLimiting("public-write")]
    public async Task<ActionResult> SubmitAppeal(
        [FromServices] ICommandHandler<SubmitAppealCommand> handler,
        string caseNumber,
        [FromBody] SubmitAppealRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(request.Website))
        {
            return Accepted(new { status = "received" });
        }

        var result = await handler.Handle(new SubmitAppealCommand(caseNumber, request.Token, request.Text), cancellationToken);
        return result.ToActionResult(this, StatusCodes.Status201Created);
    }
}

[ApiController]
[Route("api/v1/contact-requests")]
public sealed class ContactRequestsController : ControllerBase
{
    public sealed record CreateContactRequest(
        string Type,
        string Name,
        string Email,
        string Message,
        string? GymSlug,
        string? Website);

    /// <summary>Public contact/suggestion/correction form. The "website" field is a honeypot.</summary>
    [HttpPost]
    [EnableRateLimiting("public-write")]
    public async Task<ActionResult> Create(
        [FromServices] ICommandHandler<CreateContactRequestCommand, Guid> handler,
        [FromBody] CreateContactRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(request.Website))
        {
            return Accepted(new { status = "received" });
        }

        var result = await handler.Handle(
            new CreateContactRequestCommand(request.Type, request.Name, request.Email, request.Message, request.GymSlug),
            cancellationToken);
        return result.Map(id => new { id }).ToCreatedResult(this);
    }
}

[ApiController]
[Route("api/v1/analytics")]
public sealed class AnalyticsController : ControllerBase
{
    public sealed record RecordEventRequest(string EventType, string? Path, string SessionId);

    /// <summary>PII-free analytics ingestion: allowlisted event types, hashed rotating session bucket, no IP.</summary>
    [HttpPost("events")]
    [EnableRateLimiting("analytics")]
    public async Task<ActionResult> Record(
        [FromServices] ICommandHandler<RecordAnalyticsEventCommand> handler,
        [FromBody] RecordEventRequest request,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new RecordAnalyticsEventCommand(request.EventType, request.Path, request.SessionId),
            cancellationToken);
        return result.ToActionResult(this, StatusCodes.Status202Accepted);
    }
}
