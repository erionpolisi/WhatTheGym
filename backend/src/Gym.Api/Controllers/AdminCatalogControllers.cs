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

namespace Gym.Api.Controllers;

[ApiController]
[Route("api/v1/admin/gyms")]
[Authorize(Policy = "Admin")]
public sealed class AdminGymsController : ControllerBase
{
    public sealed record GymRequest(
        string Name,
        Guid? ChainId,
        int District,
        string AddressLine,
        string PostalCode,
        string? Website,
        string? Phone,
        string? Description,
        string? Status,
        IReadOnlyList<Guid>? AmenityIds,
        IReadOnlyList<OpeningHourInput>? OpeningHours);

    /// <summary>Admin search across all gyms including drafts.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<GymListItemDto>>> List(
        [FromServices] IQueryHandler<SearchGymsQuery, PagedResult<GymListItemDto>> handler,
        [FromQuery] string? term,
        [FromQuery] int? district,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new SearchGymsQuery(term, district, null, null, null, null, "name", page, pageSize, IncludeNonPublic: true),
            cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost]
    public async Task<ActionResult> Create(
        [FromServices] ICommandHandler<CreateGymCommand, Guid> handler,
        [FromBody] GymRequest request,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new CreateGymCommand(
                request.Name, request.ChainId, request.District, request.AddressLine, request.PostalCode,
                request.Website, request.Phone, request.Description, request.Status ?? "Draft",
                request.AmenityIds ?? [], request.OpeningHours ?? []),
            cancellationToken);
        return result.Map(id => new { id }).ToCreatedResult(this);
    }

    [HttpPut("{gymId:guid}")]
    public async Task<ActionResult> Update(
        [FromServices] ICommandHandler<UpdateGymCommand> handler,
        Guid gymId,
        [FromBody] GymRequest request,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new UpdateGymCommand(
                gymId, request.Name, request.ChainId, request.District, request.AddressLine, request.PostalCode,
                request.Website, request.Phone, request.Description,
                request.AmenityIds ?? [], request.OpeningHours ?? []),
            cancellationToken);
        return result.ToActionResult(this);
    }

    public sealed record StatusRequest(string Status);

    [HttpPatch("{gymId:guid}/status")]
    public async Task<ActionResult> ChangeStatus(
        [FromServices] ICommandHandler<ChangeGymStatusCommand> handler,
        Guid gymId,
        [FromBody] StatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new ChangeGymStatusCommand(gymId, request.Status), cancellationToken);
        return result.ToActionResult(this);
    }
}

[ApiController]
[Route("api/v1/admin/chains")]
[Authorize(Policy = "Admin")]
public sealed class AdminChainsController : ControllerBase
{
    public sealed record ChainRequest(string Name, string? Website);

    [HttpPost]
    public async Task<ActionResult> Create(
        [FromServices] ICommandHandler<CreateChainCommand, Guid> handler,
        [FromBody] ChainRequest request,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new CreateChainCommand(request.Name, request.Website), cancellationToken);
        return result.Map(id => new { id }).ToCreatedResult(this);
    }

    [HttpPut("{chainId:guid}")]
    public async Task<ActionResult> Update(
        [FromServices] ICommandHandler<UpdateChainCommand> handler,
        Guid chainId,
        [FromBody] ChainRequest request,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new UpdateChainCommand(chainId, request.Name, request.Website), cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpDelete("{chainId:guid}")]
    public async Task<ActionResult> Delete(
        [FromServices] ICommandHandler<DeleteChainCommand> handler,
        Guid chainId,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new DeleteChainCommand(chainId), cancellationToken);
        return result.ToActionResult(this);
    }
}

[ApiController]
[Route("api/v1/admin/amenities")]
[Authorize(Policy = "Admin")]
public sealed class AdminAmenitiesController : ControllerBase
{
    public sealed record AmenityRequest(string Name);

    [HttpPost]
    public async Task<ActionResult> Create(
        [FromServices] ICommandHandler<CreateAmenityCommand, Guid> handler,
        [FromBody] AmenityRequest request,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new CreateAmenityCommand(request.Name), cancellationToken);
        return result.Map(id => new { id }).ToCreatedResult(this);
    }

    [HttpPut("{amenityId:guid}")]
    public async Task<ActionResult> Rename(
        [FromServices] ICommandHandler<RenameAmenityCommand> handler,
        Guid amenityId,
        [FromBody] AmenityRequest request,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new RenameAmenityCommand(amenityId, request.Name), cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpDelete("{amenityId:guid}")]
    public async Task<ActionResult> Delete(
        [FromServices] ICommandHandler<DeleteAmenityCommand> handler,
        Guid amenityId,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new DeleteAmenityCommand(amenityId), cancellationToken);
        return result.ToActionResult(this);
    }
}

[ApiController]
[Route("api/v1/admin/summaries")]
[Authorize(Policy = "Admin")]
public sealed class AdminSummariesController : ControllerBase
{
    /// <summary>Rebuilds all materialized gym rating summaries from published reviews.</summary>
    [HttpPost("rebuild")]
    public async Task<ActionResult> Rebuild(
        [FromServices] ICommandHandler<RebuildSummariesCommand, int> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new RebuildSummariesCommand(), cancellationToken);
        return result.Map(count => new { rebuiltGyms = count }).ToActionResult(this);
    }
}
