using Gym.Application.Abstractions;
using Gym.Application.Common;
using Gym.Application.Contracts;
using Gym.Domain.Common;
using Gym.Domain.Entities;

namespace Gym.Application.Features.Amenities;

public sealed record CreateAmenityCommand(string Name);

public sealed record RenameAmenityCommand(Guid AmenityId, string Name);

public sealed record DeleteAmenityCommand(Guid AmenityId);

public sealed record ListAmenitiesQuery;

public sealed class CreateAmenityCommandHandler(
    IAmenityRepository amenities,
    IUnitOfWork unitOfWork,
    IClock clock) : ICommandHandler<CreateAmenityCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateAmenityCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Name) || command.Name.Trim().Length > 120)
        {
            return Result.Failure<Guid>(Error.Validation("amenity.name", "Name ist erforderlich (max. 120 Zeichen)."));
        }

        var slug = await SlugUniquifier.EnsureUniqueAsync(
            Slug.Generate(command.Name),
            s => amenities.SlugExistsAsync(s, cancellationToken));

        var amenity = Amenity.Create(command.Name, slug, clock.UtcNow);
        amenities.Add(amenity);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return amenity.Id;
    }
}

public sealed class RenameAmenityCommandHandler(
    IAmenityRepository amenities,
    IUnitOfWork unitOfWork) : ICommandHandler<RenameAmenityCommand>
{
    public async Task<Result> Handle(RenameAmenityCommand command, CancellationToken cancellationToken)
    {
        var amenity = await amenities.GetByIdAsync(command.AmenityId, cancellationToken);
        if (amenity is null)
        {
            return Result.Failure(Error.NotFound("amenity.notFound", "Die Ausstattung wurde nicht gefunden."));
        }

        if (string.IsNullOrWhiteSpace(command.Name) || command.Name.Trim().Length > 120)
        {
            return Result.Failure(Error.Validation("amenity.name", "Name ist erforderlich (max. 120 Zeichen)."));
        }

        amenity.Rename(command.Name);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed class DeleteAmenityCommandHandler(
    IAmenityRepository amenities,
    IUnitOfWork unitOfWork) : ICommandHandler<DeleteAmenityCommand>
{
    public async Task<Result> Handle(DeleteAmenityCommand command, CancellationToken cancellationToken)
    {
        var amenity = await amenities.GetByIdAsync(command.AmenityId, cancellationToken);
        if (amenity is null)
        {
            return Result.Failure(Error.NotFound("amenity.notFound", "Die Ausstattung wurde nicht gefunden."));
        }

        if (await amenities.CountGymsUsingAsync(amenity.Id, cancellationToken) > 0)
        {
            return Result.Failure(Error.Conflict("amenity.inUse", "Die Ausstattung wird noch von Studios verwendet."));
        }

        amenities.Remove(amenity);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed class ListAmenitiesQueryHandler(IAmenityRepository amenities) : IQueryHandler<ListAmenitiesQuery, IReadOnlyList<AmenityDto>>
{
    public async Task<Result<IReadOnlyList<AmenityDto>>> Handle(ListAmenitiesQuery query, CancellationToken cancellationToken)
    {
        var all = await amenities.ListAllAsync(cancellationToken);
        return Result.Success<IReadOnlyList<AmenityDto>>(
            all.Select(a => new AmenityDto(a.Id, a.Name, a.Slug)).ToList());
    }
}
