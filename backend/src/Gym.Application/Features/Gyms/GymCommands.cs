using FluentValidation;
using Gym.Application.Abstractions;
using Gym.Application.Common;
using Gym.Application.Contracts;
using Gym.Domain.Common;
using Gym.Domain.Entities;
using Gym.Domain.Enums;

namespace Gym.Application.Features.Gyms;

public sealed record OpeningHourInput(int IsoDayOfWeek, string OpensAt, string ClosesAt);

public sealed record CreateGymCommand(
    string Name,
    Guid? ChainId,
    int District,
    string AddressLine,
    string PostalCode,
    string? Website,
    string? Phone,
    string? Description,
    string Status,
    IReadOnlyList<Guid> AmenityIds,
    IReadOnlyList<OpeningHourInput> OpeningHours);

public sealed record UpdateGymCommand(
    Guid GymId,
    string Name,
    Guid? ChainId,
    int District,
    string AddressLine,
    string PostalCode,
    string? Website,
    string? Phone,
    string? Description,
    IReadOnlyList<Guid> AmenityIds,
    IReadOnlyList<OpeningHourInput> OpeningHours);

public sealed record ChangeGymStatusCommand(Guid GymId, string Status);

public sealed class CreateGymCommandValidator : AbstractValidator<CreateGymCommand>
{
    public CreateGymCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(200).WithMessage("Name ist erforderlich (max. 200 Zeichen).");
        RuleFor(c => c.District).InclusiveBetween(1, 23).WithMessage("Der Bezirk muss zwischen 1 und 23 liegen.");
        RuleFor(c => c.AddressLine).NotEmpty().MaximumLength(300).WithMessage("Adresse ist erforderlich (max. 300 Zeichen).");
        RuleFor(c => c.PostalCode).NotEmpty().Matches("^1[0-9]{3}$").WithMessage("Die Postleitzahl muss eine Wiener PLZ sein (1xxx).");
        RuleFor(c => c.Website).Must(BeAbsoluteHttpUrl).When(c => !string.IsNullOrWhiteSpace(c.Website))
            .WithMessage("Die Website muss eine absolute http(s)-URL sein.");
        RuleFor(c => c.Phone).MaximumLength(40);
        RuleFor(c => c.Description).MaximumLength(2000);
    }

    internal static bool BeAbsoluteHttpUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}

public sealed class UpdateGymCommandValidator : AbstractValidator<UpdateGymCommand>
{
    public UpdateGymCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(200).WithMessage("Name ist erforderlich (max. 200 Zeichen).");
        RuleFor(c => c.District).InclusiveBetween(1, 23).WithMessage("Der Bezirk muss zwischen 1 und 23 liegen.");
        RuleFor(c => c.AddressLine).NotEmpty().MaximumLength(300).WithMessage("Adresse ist erforderlich (max. 300 Zeichen).");
        RuleFor(c => c.PostalCode).NotEmpty().Matches("^1[0-9]{3}$").WithMessage("Die Postleitzahl muss eine Wiener PLZ sein (1xxx).");
        RuleFor(c => c.Website).Must(CreateGymCommandValidator.BeAbsoluteHttpUrl).When(c => !string.IsNullOrWhiteSpace(c.Website))
            .WithMessage("Die Website muss eine absolute http(s)-URL sein.");
        RuleFor(c => c.Phone).MaximumLength(40);
        RuleFor(c => c.Description).MaximumLength(2000);
    }
}

public sealed class CreateGymCommandHandler(
    IGymRepository gyms,
    IGymChainRepository chains,
    IAmenityRepository amenities,
    ISearchIndex searchIndex,
    IUnitOfWork unitOfWork,
    IClock clock,
    IValidator<CreateGymCommand> validator) : ICommandHandler<CreateGymCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateGymCommand command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<Guid>(validation.ToError());
        }

        if (!Enum.TryParse<GymStatus>(command.Status, ignoreCase: true, out var status))
        {
            return Result.Failure<Guid>(Error.Validation("gym.status", "Ungueltiger Status."));
        }

        if (command.ChainId is Guid chainId && await chains.GetByIdAsync(chainId, cancellationToken) is null)
        {
            return Result.Failure<Guid>(Error.NotFound("chain.notFound", "Die angegebene Kette existiert nicht."));
        }

        var slug = await SlugUniquifier.EnsureUniqueAsync(
            Slug.Generate(command.Name),
            s => gyms.SlugExistsAsync(s, cancellationToken));

        var gymResult = GymEntry.Create(
            command.Name, slug, command.ChainId, command.District, command.AddressLine, command.PostalCode,
            command.Website, command.Phone, command.Description, status, clock.UtcNow);
        if (gymResult.IsFailure)
        {
            return Result.Failure<Guid>(gymResult.Error);
        }

        var gym = gymResult.Value;

        var amenitySetResult = await ApplyAmenitiesAndHours(gym, command.AmenityIds, command.OpeningHours, amenities, clock, cancellationToken);
        if (amenitySetResult.IsFailure)
        {
            return Result.Failure<Guid>(amenitySetResult.Error);
        }

        gyms.Add(gym);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await searchIndex.IndexGymAsync(gym.Id, cancellationToken);
        return gym.Id;
    }

    internal static async Task<Result> ApplyAmenitiesAndHours(
        GymEntry gym,
        IReadOnlyList<Guid> amenityIds,
        IReadOnlyList<OpeningHourInput> hours,
        IAmenityRepository amenities,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (amenityIds.Count > 0)
        {
            var found = await amenities.GetByIdsAsync(amenityIds, cancellationToken);
            if (found.Count != amenityIds.Distinct().Count())
            {
                return Result.Failure(Error.NotFound("amenity.notFound", "Mindestens eine Ausstattung existiert nicht."));
            }
        }

        gym.SetAmenities(amenityIds, clock.UtcNow);

        var openingHours = new List<GymOpeningHour>();
        foreach (var input in hours)
        {
            if (!TimeOnly.TryParse(input.OpensAt, out var opens) || !TimeOnly.TryParse(input.ClosesAt, out var closes))
            {
                return Result.Failure(Error.Validation("openingHours.format", "Oeffnungszeiten muessen im Format HH:mm angegeben werden."));
            }

            var hourResult = GymOpeningHour.Create(input.IsoDayOfWeek, opens, closes);
            if (hourResult.IsFailure)
            {
                return Result.Failure(hourResult.Error);
            }

            openingHours.Add(hourResult.Value);
        }

        gym.SetOpeningHours(openingHours, clock.UtcNow);
        return Result.Success();
    }
}

public sealed class UpdateGymCommandHandler(
    IGymRepository gyms,
    IGymChainRepository chains,
    IAmenityRepository amenities,
    ISearchIndex searchIndex,
    IUnitOfWork unitOfWork,
    IClock clock,
    IValidator<UpdateGymCommand> validator) : ICommandHandler<UpdateGymCommand>
{
    public async Task<Result> Handle(UpdateGymCommand command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure(validation.ToError());
        }

        var gym = await gyms.GetByIdAsync(command.GymId, cancellationToken);
        if (gym is null)
        {
            return Result.Failure(Error.NotFound("gym.notFound", "Das Studio wurde nicht gefunden."));
        }

        if (command.ChainId is Guid chainId && await chains.GetByIdAsync(chainId, cancellationToken) is null)
        {
            return Result.Failure(Error.NotFound("chain.notFound", "Die angegebene Kette existiert nicht."));
        }

        var updateResult = gym.Update(
            command.Name, command.ChainId, command.District, command.AddressLine, command.PostalCode,
            command.Website, command.Phone, command.Description, clock.UtcNow);
        if (updateResult.IsFailure)
        {
            return updateResult;
        }

        var applyResult = await CreateGymCommandHandler.ApplyAmenitiesAndHours(
            gym, command.AmenityIds, command.OpeningHours, amenities, clock, cancellationToken);
        if (applyResult.IsFailure)
        {
            return applyResult;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await searchIndex.IndexGymAsync(gym.Id, cancellationToken);
        return Result.Success();
    }
}

public sealed class ChangeGymStatusCommandHandler(
    IGymRepository gyms,
    IUnitOfWork unitOfWork,
    IClock clock) : ICommandHandler<ChangeGymStatusCommand>
{
    public async Task<Result> Handle(ChangeGymStatusCommand command, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<GymStatus>(command.Status, ignoreCase: true, out var status))
        {
            return Result.Failure(Error.Validation("gym.status", "Ungueltiger Status."));
        }

        var gym = await gyms.GetByIdAsync(command.GymId, cancellationToken);
        if (gym is null)
        {
            return Result.Failure(Error.NotFound("gym.notFound", "Das Studio wurde nicht gefunden."));
        }

        gym.ChangeStatus(status, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
