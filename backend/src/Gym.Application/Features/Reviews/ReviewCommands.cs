using System.Text.Json;
using FluentValidation;
using Gym.Application.Abstractions;
using Gym.Application.Common;
using Gym.Application.Contracts;
using Gym.Domain.Common;
using Gym.Domain.Entities;
using Gym.Domain.Enums;
using Gym.Domain.Scoring;

namespace Gym.Application.Features.Reviews;

/// <summary>Recomputes and materializes the rating summary of a gym from its published reviews.</summary>
public sealed class GymScoreUpdater(IReviewRepository reviews, IGymRatingSummaryStore summaries)
{
    public async Task RecalculateAsync(Guid gymId, CancellationToken cancellationToken)
    {
        var ratings = await reviews.GetPublishedRatingsAsync(gymId, cancellationToken);
        var score = ScoreCalculator.Calculate(ratings);
        await summaries.UpsertAsync(gymId, score, cancellationToken);
    }
}

public sealed record CreateReviewCommand(Guid UserId, string GymSlug, RatingsDto Ratings, string? Text);

public sealed record UpdateOwnReviewCommand(Guid UserId, Guid ReviewId, RatingsDto Ratings, string? Text);

public sealed record DeleteOwnReviewCommand(Guid UserId, Guid ReviewId);

public sealed class CreateReviewCommandValidator : AbstractValidator<CreateReviewCommand>
{
    public CreateReviewCommandValidator()
    {
        RuleFor(c => c.GymSlug).NotEmpty();
        RuleFor(c => c.Text).MaximumLength(Review.MaxTextLength)
            .WithMessage($"Der Text darf hoechstens {Review.MaxTextLength} Zeichen lang sein.");
        RuleFor(c => c.Ratings).Must(HaveAtLeastOneRating)
            .WithMessage("Mindestens eine Kategorie muss mit 1 bis 5 bewertet werden.");
        RuleFor(c => c.Ratings).Must(AllRatingsInRange)
            .WithMessage("Alle Bewertungen muessen zwischen 1 und 5 liegen.");
        RuleFor(c => c.Text).Must(NotContainTooManyLinks)
            .WithMessage("Der Text enthaelt zu viele Links.");
    }

    internal static bool HaveAtLeastOneRating(RatingsDto ratings) => ratings.ToDomain().HasAnyRating;

    internal static bool AllRatingsInRange(RatingsDto ratings) => ratings.ToDomain().AllWithinRange;

    internal static bool NotContainTooManyLinks(string? text) =>
        text is null || CountOccurrences(text, "http") <= 3;

    private static int CountOccurrences(string text, string token)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(token, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            count++;
            index += token.Length;
        }

        return count;
    }
}

/// <summary>Edits must satisfy the same content rules as new reviews (incl. the link-spam check).</summary>
public sealed class UpdateOwnReviewCommandValidator : AbstractValidator<UpdateOwnReviewCommand>
{
    public UpdateOwnReviewCommandValidator()
    {
        RuleFor(c => c.Text).MaximumLength(Review.MaxTextLength)
            .WithMessage($"Der Text darf hoechstens {Review.MaxTextLength} Zeichen lang sein.");
        RuleFor(c => c.Ratings).Must(CreateReviewCommandValidator.HaveAtLeastOneRating)
            .WithMessage("Mindestens eine Kategorie muss mit 1 bis 5 bewertet werden.");
        RuleFor(c => c.Ratings).Must(CreateReviewCommandValidator.AllRatingsInRange)
            .WithMessage("Alle Bewertungen muessen zwischen 1 und 5 liegen.");
        RuleFor(c => c.Text).Must(CreateReviewCommandValidator.NotContainTooManyLinks)
            .WithMessage("Der Text enthaelt zu viele Links.");
    }
}

public sealed class CreateReviewCommandHandler(
    IReviewRepository reviews,
    IGymRepository gyms,
    IUserRepository users,
    GymScoreUpdater scoreUpdater,
    IUnitOfWork unitOfWork,
    IClock clock,
    IValidator<CreateReviewCommand> validator) : ICommandHandler<CreateReviewCommand, OwnReviewDto>
{
    public async Task<Result<OwnReviewDto>> Handle(CreateReviewCommand command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<OwnReviewDto>(validation.ToError());
        }

        var user = await users.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null || user.Status != UserStatus.Active)
        {
            return Result.Failure<OwnReviewDto>(Error.Unauthorized("auth.required", "Anmeldung erforderlich."));
        }

        if (!user.EmailVerified)
        {
            return Result.Failure<OwnReviewDto>(Error.Forbidden(
                "review.unverified", "Nur ueber Google verifizierte Konten koennen Bewertungen schreiben."));
        }

        var gym = await gyms.GetBySlugAsync(command.GymSlug, cancellationToken);
        if (gym is null || !gym.IsPubliclyVisible)
        {
            return Result.Failure<OwnReviewDto>(Error.NotFound("gym.notFound", "Das Studio wurde nicht gefunden."));
        }

        if (!gym.AcceptsReviews)
        {
            return Result.Failure<OwnReviewDto>(Error.Conflict("gym.closed", "Dieses Studio kann derzeit nicht bewertet werden."));
        }

        if (await reviews.HasActiveReviewAsync(gym.Id, user.Id, cancellationToken))
        {
            return Result.Failure<OwnReviewDto>(Error.Conflict(
                "review.exists", "Du hast dieses Studio bereits bewertet. Bearbeite deine bestehende Bewertung."));
        }

        var reviewResult = Review.Create(gym.Id, user.Id, command.Ratings.ToDomain(), command.Text, clock.UtcNow);
        if (reviewResult.IsFailure)
        {
            return Result.Failure<OwnReviewDto>(reviewResult.Error);
        }

        var review = reviewResult.Value;
        reviews.Add(review);
        await scoreUpdater.RecalculateAsync(gym.Id, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToOwnDto(review);
    }

    internal static OwnReviewDto ToOwnDto(Review review) => new(
        review.Id,
        review.GymId,
        review.Status.ToString(),
        RatingsDto.From(review.Ratings),
        review.Text,
        review.EditCount,
        review.CreatedAtUtc,
        review.UpdatedAtUtc);
}

public sealed class UpdateOwnReviewCommandHandler(
    IReviewRepository reviews,
    GymScoreUpdater scoreUpdater,
    IUnitOfWork unitOfWork,
    IClock clock,
    IValidator<UpdateOwnReviewCommand> validator) : ICommandHandler<UpdateOwnReviewCommand, OwnReviewDto>
{
    public async Task<Result<OwnReviewDto>> Handle(UpdateOwnReviewCommand command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<OwnReviewDto>(validation.ToError());
        }

        var review = await reviews.GetByIdAsync(command.ReviewId, cancellationToken);
        if (review is null || review.UserId != command.UserId)
        {
            return Result.Failure<OwnReviewDto>(Error.NotFound("review.notFound", "Die Bewertung wurde nicht gefunden."));
        }

        var snapshot = ReviewRevision.Create(
            review.Id,
            review.EditCount + 1,
            review.Text,
            JsonSerializer.Serialize(RatingsDto.From(review.Ratings), SummaryJson.Options),
            command.UserId,
            clock.UtcNow);

        var editResult = review.Edit(command.Ratings.ToDomain(), command.Text, clock.UtcNow);
        if (editResult.IsFailure)
        {
            return Result.Failure<OwnReviewDto>(editResult.Error);
        }

        reviews.AddRevision(snapshot);
        await scoreUpdater.RecalculateAsync(review.GymId, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return CreateReviewCommandHandler.ToOwnDto(review);
    }
}

public sealed class DeleteOwnReviewCommandHandler(
    IReviewRepository reviews,
    GymScoreUpdater scoreUpdater,
    IUnitOfWork unitOfWork,
    IClock clock) : ICommandHandler<DeleteOwnReviewCommand>
{
    public async Task<Result> Handle(DeleteOwnReviewCommand command, CancellationToken cancellationToken)
    {
        var review = await reviews.GetByIdAsync(command.ReviewId, cancellationToken);
        if (review is null || review.UserId != command.UserId)
        {
            return Result.Failure(Error.NotFound("review.notFound", "Die Bewertung wurde nicht gefunden."));
        }

        var deleteResult = review.SoftDelete(ReviewDeletionOrigin.Author, null, clock.UtcNow);
        if (deleteResult.IsFailure)
        {
            return deleteResult;
        }

        await scoreUpdater.RecalculateAsync(review.GymId, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal static class SummaryJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.General);
}
