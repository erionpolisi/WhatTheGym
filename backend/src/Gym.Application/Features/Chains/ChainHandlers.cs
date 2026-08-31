using Gym.Application.Abstractions;
using Gym.Application.Common;
using Gym.Application.Contracts;
using Gym.Application.Features.Gyms;
using Gym.Domain.Common;
using Gym.Domain.Entities;

namespace Gym.Application.Features.Chains;

public sealed record CreateChainCommand(string Name, string? Website);

public sealed record UpdateChainCommand(Guid ChainId, string Name, string? Website);

public sealed record DeleteChainCommand(Guid ChainId);

public sealed record ListChainsQuery;

public sealed class CreateChainCommandHandler(
    IGymChainRepository chains,
    IUnitOfWork unitOfWork,
    IClock clock) : ICommandHandler<CreateChainCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateChainCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Name) || command.Name.Trim().Length > 200)
        {
            return Result.Failure<Guid>(Error.Validation("chain.name", "Name ist erforderlich (max. 200 Zeichen)."));
        }

        if (!string.IsNullOrWhiteSpace(command.Website) && !CreateGymCommandValidator.BeAbsoluteHttpUrl(command.Website))
        {
            return Result.Failure<Guid>(Error.Validation("chain.website", "Die Website muss eine absolute http(s)-URL sein."));
        }

        var slug = await SlugUniquifier.EnsureUniqueAsync(
            Slug.Generate(command.Name),
            s => chains.SlugExistsAsync(s, cancellationToken));

        var chain = GymChain.Create(command.Name, slug, command.Website, clock.UtcNow);
        chains.Add(chain);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return chain.Id;
    }
}

public sealed class UpdateChainCommandHandler(
    IGymChainRepository chains,
    IUnitOfWork unitOfWork,
    IClock clock) : ICommandHandler<UpdateChainCommand>
{
    public async Task<Result> Handle(UpdateChainCommand command, CancellationToken cancellationToken)
    {
        var chain = await chains.GetByIdAsync(command.ChainId, cancellationToken);
        if (chain is null)
        {
            return Result.Failure(Error.NotFound("chain.notFound", "Die Kette wurde nicht gefunden."));
        }

        if (string.IsNullOrWhiteSpace(command.Name) || command.Name.Trim().Length > 200)
        {
            return Result.Failure(Error.Validation("chain.name", "Name ist erforderlich (max. 200 Zeichen)."));
        }

        if (!string.IsNullOrWhiteSpace(command.Website) && !CreateGymCommandValidator.BeAbsoluteHttpUrl(command.Website))
        {
            return Result.Failure(Error.Validation("chain.website", "Die Website muss eine absolute http(s)-URL sein."));
        }

        chain.Update(command.Name, command.Website, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed class DeleteChainCommandHandler(
    IGymChainRepository chains,
    IUnitOfWork unitOfWork) : ICommandHandler<DeleteChainCommand>
{
    public async Task<Result> Handle(DeleteChainCommand command, CancellationToken cancellationToken)
    {
        var chain = await chains.GetByIdAsync(command.ChainId, cancellationToken);
        if (chain is null)
        {
            return Result.Failure(Error.NotFound("chain.notFound", "Die Kette wurde nicht gefunden."));
        }

        if (await chains.CountGymsAsync(chain.Id, cancellationToken) > 0)
        {
            return Result.Failure(Error.Conflict("chain.inUse", "Die Kette hat noch zugeordnete Studios und kann nicht geloescht werden."));
        }

        chains.Remove(chain);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed class ListChainsQueryHandler(IGymChainRepository chains) : IQueryHandler<ListChainsQuery, IReadOnlyList<ChainDto>>
{
    public async Task<Result<IReadOnlyList<ChainDto>>> Handle(ListChainsQuery query, CancellationToken cancellationToken)
    {
        var all = await chains.ListAllAsync(cancellationToken);
        return Result.Success<IReadOnlyList<ChainDto>>(
            all.Select(c => new ChainDto(c.Id, c.Name, c.Slug, c.Website)).ToList());
    }
}
