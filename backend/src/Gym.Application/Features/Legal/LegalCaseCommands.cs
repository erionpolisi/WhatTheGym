using System.Text.Json;
using FluentValidation;
using Gym.Application.Abstractions;
using Gym.Application.Common;
using Gym.Application.Contracts;
using Gym.Application.Features.Reviews;
using Gym.Domain.Common;
using Gym.Domain.Entities;
using Gym.Domain.Enums;
using Microsoft.Extensions.Options;

namespace Gym.Application.Features.Legal;

internal static class CaseEvents
{
    public static async Task AppendAsync(
        ILegalCaseRepository cases,
        IClock clock,
        Guid caseId,
        LegalCaseEventType type,
        LegalActorType actorType,
        Guid? actorId,
        object data,
        CancellationToken cancellationToken)
    {
        var sequence = await cases.NextEventSequenceAsync(caseId, cancellationToken);
        cases.AddEvent(LegalCaseEvent.Create(
            caseId, sequence, type, actorType, actorId,
            JsonSerializer.Serialize(data, SummaryJson.Options), clock.UtcNow));
    }
}

public static class LegalLinks
{
    public static string CaseStatusUrl(string baseUrl, string caseNumber, string token) =>
        $"{baseUrl.TrimEnd('/')}/rechtliches/fall/{Uri.EscapeDataString(caseNumber)}?token={Uri.EscapeDataString(token)}";

    public static string AppealUrl(string baseUrl, string caseNumber, string token) =>
        $"{baseUrl.TrimEnd('/')}/rechtliches/einspruch/{Uri.EscapeDataString(caseNumber)}?token={Uri.EscapeDataString(token)}";
}

public sealed record ReportReviewCommand(
    Guid ReviewId,
    string Category,
    string ReporterName,
    string ReporterEmail,
    string Description);

public sealed class ReportReviewCommandValidator : AbstractValidator<ReportReviewCommand>
{
    public ReportReviewCommandValidator()
    {
        RuleFor(c => c.ReporterName).NotEmpty().MaximumLength(120).WithMessage("Name ist erforderlich (max. 120 Zeichen).");
        RuleFor(c => c.ReporterEmail).NotEmpty().EmailAddress().MaximumLength(254).WithMessage("Eine gueltige E-Mail-Adresse ist erforderlich.");
        RuleFor(c => c.Description).NotEmpty().MinimumLength(20).MaximumLength(LegalCase.MaxDescriptionLength)
            .WithMessage($"Die Begruendung muss zwischen 20 und {LegalCase.MaxDescriptionLength} Zeichen lang sein.");
    }
}

public sealed class ReportReviewCommandHandler(
    IReviewRepository reviews,
    ILegalCaseRepository cases,
    ISecureTokenService tokens,
    IEmailOutbox outbox,
    IUnitOfWork unitOfWork,
    IClock clock,
    IOptions<MailOptions> mailOptions,
    IValidator<ReportReviewCommand> validator) : ICommandHandler<ReportReviewCommand, ReportReviewResultDto>
{
    public async Task<Result<ReportReviewResultDto>> Handle(ReportReviewCommand command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<ReportReviewResultDto>(validation.ToError());
        }

        if (!Enum.TryParse<LegalCaseCategory>(command.Category, ignoreCase: true, out var category))
        {
            return Result.Failure<ReportReviewResultDto>(Error.Validation("legalCase.category", "Ungueltige Meldekategorie."));
        }

        var review = await reviews.GetByIdAsync(command.ReviewId, cancellationToken);
        if (review is null || review.Status is not (ReviewStatus.Published or ReviewStatus.UnderReview))
        {
            return Result.Failure<ReportReviewResultDto>(Error.NotFound("review.notFound", "Die Bewertung wurde nicht gefunden."));
        }

        var viennaYear = TimeZoneInfo.ConvertTime(clock.UtcNow, ViennaTime.Zone).Year;
        var caseNumber = await cases.NextCaseNumberAsync(viennaYear, cancellationToken);
        var (statusToken, statusTokenHash) = tokens.CreateToken();

        var caseResult = LegalCase.Create(
            caseNumber, review.Id, category, command.ReporterName, command.ReporterEmail,
            command.Description, statusTokenHash, clock.UtcNow);
        if (caseResult.IsFailure)
        {
            return Result.Failure<ReportReviewResultDto>(caseResult.Error);
        }

        var legalCase = caseResult.Value;
        cases.Add(legalCase);

        await CaseEvents.AppendAsync(cases, clock, legalCase.Id, LegalCaseEventType.CaseCreated, LegalActorType.Reporter, null,
            new { caseNumber, reviewId = review.Id, category = category.ToString() }, cancellationToken);

        var statusUrl = LegalLinks.CaseStatusUrl(mailOptions.Value.PublicBaseUrl, caseNumber, statusToken);
        var (subject, body) = LegalMailTexts.ReportReceived(caseNumber, statusUrl);
        outbox.Enqueue(OutboxEmail.Enqueue(legalCase.ReporterEmail, subject, body, "legal.reportReceived", legalCase.Id, clock.UtcNow));

        await CaseEvents.AppendAsync(cases, clock, legalCase.Id, LegalCaseEventType.NotificationQueued, LegalActorType.System, null,
            new { to = "reporter", subject, body }, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new ReportReviewResultDto(caseNumber, statusToken);
    }
}

public static class ViennaTime
{
    public static readonly TimeZoneInfo Zone = ResolveZone();

    private static TimeZoneInfo ResolveZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
        }
    }
}

public sealed record ClassifyCaseCommand(Guid ActorId, Guid CaseId, string Classification);

public sealed class ClassifyCaseCommandHandler(
    ILegalCaseRepository cases,
    IReviewRepository reviews,
    IUserRepository users,
    GymScoreUpdater scoreUpdater,
    IEmailOutbox outbox,
    IUnitOfWork unitOfWork,
    IClock clock) : ICommandHandler<ClassifyCaseCommand>
{
    public async Task<Result> Handle(ClassifyCaseCommand command, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<LegalCaseClassification>(command.Classification, ignoreCase: true, out var classification))
        {
            return Result.Failure(Error.Validation("legalCase.classification", "Ungueltige Klassifizierung."));
        }

        var legalCase = await cases.GetByIdAsync(command.CaseId, cancellationToken);
        if (legalCase is null)
        {
            return Result.Failure(Error.NotFound("legalCase.notFound", "Der Fall wurde nicht gefunden."));
        }

        var classifyResult = legalCase.Classify(classification, clock.UtcNow);
        if (classifyResult.IsFailure)
        {
            return classifyResult;
        }

        await CaseEvents.AppendAsync(cases, clock, legalCase.Id, LegalCaseEventType.Classified, LegalActorType.Admin, command.ActorId,
            new { classification = classification.ToString() }, cancellationToken);

        // Only an explicitly classified obviously-illegal fast-track case hides content before the decision.
        if (classification == LegalCaseClassification.FastTrackObviouslyIllegal)
        {
            var review = await reviews.GetByIdAsync(legalCase.ReviewId, cancellationToken);
            if (review is not null && review.Status == ReviewStatus.Published)
            {
                var hideResult = review.PlaceUnderLegalReview(clock.UtcNow);
                if (hideResult.IsFailure)
                {
                    return hideResult;
                }

                await scoreUpdater.RecalculateAsync(review.GymId, cancellationToken);
                await CaseEvents.AppendAsync(cases, clock, legalCase.Id, LegalCaseEventType.ContentHidden, LegalActorType.System, null,
                    new { reviewId = review.Id }, cancellationToken);

                var author = await users.GetByIdAsync(review.UserId, cancellationToken);
                if (author is not null && author.Status == UserStatus.Active)
                {
                    var (subject, body) = LegalMailTexts.ContentHiddenFastTrack(legalCase.CaseNumber);
                    outbox.Enqueue(OutboxEmail.Enqueue(author.Email, subject, body, "legal.contentHidden", legalCase.Id, clock.UtcNow));
                    await CaseEvents.AppendAsync(cases, clock, legalCase.Id, LegalCaseEventType.NotificationQueued, LegalActorType.System, null,
                        new { to = "author", subject, body }, cancellationToken);
                }
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record StartCaseReviewCommand(Guid ActorId, Guid CaseId);

public sealed class StartCaseReviewCommandHandler(
    ILegalCaseRepository cases,
    IUnitOfWork unitOfWork,
    IClock clock) : ICommandHandler<StartCaseReviewCommand>
{
    public async Task<Result> Handle(StartCaseReviewCommand command, CancellationToken cancellationToken)
    {
        var legalCase = await cases.GetByIdAsync(command.CaseId, cancellationToken);
        if (legalCase is null)
        {
            return Result.Failure(Error.NotFound("legalCase.notFound", "Der Fall wurde nicht gefunden."));
        }

        var result = legalCase.StartReview(clock.UtcNow);
        if (result.IsFailure)
        {
            return result;
        }

        await CaseEvents.AppendAsync(cases, clock, legalCase.Id, LegalCaseEventType.ReviewStarted, LegalActorType.Admin, command.ActorId,
            new { }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record DecideCaseCommand(Guid ActorId, Guid CaseId, string Decision, string Rationale);

public sealed class DecideCaseCommandHandler(
    ILegalCaseRepository cases,
    IReviewRepository reviews,
    IUserRepository users,
    ISecureTokenService tokens,
    GymScoreUpdater scoreUpdater,
    IEmailOutbox outbox,
    IUnitOfWork unitOfWork,
    IClock clock,
    IOptions<MailOptions> mailOptions) : ICommandHandler<DecideCaseCommand>
{
    public async Task<Result> Handle(DecideCaseCommand command, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<LegalDecision>(command.Decision, ignoreCase: true, out var decision))
        {
            return Result.Failure(Error.Validation("legalCase.decision", "Ungueltige Entscheidung (KeepOnline oder FullyRemoved)."));
        }

        var legalCase = await cases.GetByIdAsync(command.CaseId, cancellationToken);
        if (legalCase is null)
        {
            return Result.Failure(Error.NotFound("legalCase.notFound", "Der Fall wurde nicht gefunden."));
        }

        var review = await reviews.GetByIdAsync(legalCase.ReviewId, cancellationToken);
        if (review is null)
        {
            return Result.Failure(Error.NotFound("review.notFound", "Die zugehoerige Bewertung wurde nicht gefunden."));
        }

        var decideResult = legalCase.Decide(decision, command.Rationale, clock.UtcNow);
        if (decideResult.IsFailure)
        {
            return decideResult;
        }

        var removed = decision == LegalDecision.FullyRemoved;
        if (removed)
        {
            review.RemoveLegal(clock.UtcNow);
            await scoreUpdater.RecalculateAsync(review.GymId, cancellationToken);
        }
        else if (review.Status == ReviewStatus.UnderReview)
        {
            var release = review.ReleaseFromLegalReview(clock.UtcNow);
            if (release.IsFailure)
            {
                return release;
            }

            await scoreUpdater.RecalculateAsync(review.GymId, cancellationToken);
        }

        await CaseEvents.AppendAsync(cases, clock, legalCase.Id, LegalCaseEventType.Decided, LegalActorType.Admin, command.ActorId,
            new { decision = decision.ToString(), rationale = command.Rationale }, cancellationToken);

        // The appeal token goes to the adversely affected party.
        var (appealToken, appealTokenHash) = tokens.CreateToken();
        legalCase.SetAppealTokenHash(appealTokenHash, clock.UtcNow);
        var baseUrl = mailOptions.Value.PublicBaseUrl;
        var appealUrl = LegalLinks.AppealUrl(baseUrl, legalCase.CaseNumber, appealToken);
        var statusUrl = LegalLinks.CaseStatusUrl(baseUrl, legalCase.CaseNumber, "***");

        var (reporterSubject, reporterBody) = LegalMailTexts.DecisionToReporter(
            legalCase.CaseNumber, removed, statusUrl, removed ? null : appealUrl);
        outbox.Enqueue(OutboxEmail.Enqueue(legalCase.ReporterEmail, reporterSubject, reporterBody, "legal.decision.reporter", legalCase.Id, clock.UtcNow));
        await CaseEvents.AppendAsync(cases, clock, legalCase.Id, LegalCaseEventType.NotificationQueued, LegalActorType.System, null,
            new { to = "reporter", subject = reporterSubject, body = reporterBody }, cancellationToken);

        var author = await users.GetByIdAsync(review.UserId, cancellationToken);
        if (author is not null && author.Status == UserStatus.Active)
        {
            var (authorSubject, authorBody) = LegalMailTexts.DecisionToAuthor(
                legalCase.CaseNumber, removed, removed ? appealUrl : null);
            outbox.Enqueue(OutboxEmail.Enqueue(author.Email, authorSubject, authorBody, "legal.decision.author", legalCase.Id, clock.UtcNow));
            await CaseEvents.AppendAsync(cases, clock, legalCase.Id, LegalCaseEventType.NotificationQueued, LegalActorType.System, null,
                new { to = "author", subject = authorSubject, body = authorBody }, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record CloseCaseCommand(Guid ActorId, Guid CaseId);

public sealed class CloseCaseCommandHandler(
    ILegalCaseRepository cases,
    IUnitOfWork unitOfWork,
    IClock clock) : ICommandHandler<CloseCaseCommand>
{
    public async Task<Result> Handle(CloseCaseCommand command, CancellationToken cancellationToken)
    {
        var legalCase = await cases.GetByIdAsync(command.CaseId, cancellationToken);
        if (legalCase is null)
        {
            return Result.Failure(Error.NotFound("legalCase.notFound", "Der Fall wurde nicht gefunden."));
        }

        var result = legalCase.Close(clock.UtcNow);
        if (result.IsFailure)
        {
            return result;
        }

        await CaseEvents.AppendAsync(cases, clock, legalCase.Id, LegalCaseEventType.Closed, LegalActorType.Admin, command.ActorId,
            new { }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record SubmitAppealCommand(string CaseNumber, string Token, string Text);

public sealed class SubmitAppealCommandHandler(
    ILegalCaseRepository cases,
    IReviewRepository reviews,
    IUserRepository users,
    ISecureTokenService tokens,
    IEmailOutbox outbox,
    IUnitOfWork unitOfWork,
    IClock clock) : ICommandHandler<SubmitAppealCommand>
{
    public async Task<Result> Handle(SubmitAppealCommand command, CancellationToken cancellationToken)
    {
        var legalCase = await cases.GetByCaseNumberAsync(command.CaseNumber, cancellationToken);
        if (legalCase is null || legalCase.AppealTokenHash is null
            || !string.Equals(legalCase.AppealTokenHash, tokens.Hash(command.Token), StringComparison.Ordinal))
        {
            return Result.Failure(Error.NotFound("appeal.invalid", "Der Fall wurde nicht gefunden oder der Link ist ungueltig."));
        }

        if (!legalCase.IsAppealOpen(clock.UtcNow))
        {
            return Result.Failure(Error.Conflict("appeal.closed", "Die Einspruchsfrist ist abgelaufen."));
        }

        var appealResult = LegalCaseAppeal.Create(legalCase.Id, command.Text, clock.UtcNow);
        if (appealResult.IsFailure)
        {
            return Result.Failure(appealResult.Error);
        }

        cases.AddAppeal(appealResult.Value);
        await CaseEvents.AppendAsync(cases, clock, legalCase.Id, LegalCaseEventType.AppealSubmitted, LegalActorType.Author, null,
            new { appealId = appealResult.Value.Id }, cancellationToken);

        // Confirmation goes to the party the appeal token was issued to.
        var recipient = legalCase.Decision == LegalDecision.FullyRemoved
            ? (await GetAuthorEmailAsync(legalCase.ReviewId, cancellationToken))
            : legalCase.ReporterEmail;
        if (recipient is not null)
        {
            var (subject, body) = LegalMailTexts.AppealReceived(legalCase.CaseNumber);
            outbox.Enqueue(OutboxEmail.Enqueue(recipient, subject, body, "legal.appealReceived", legalCase.Id, clock.UtcNow));
            await CaseEvents.AppendAsync(cases, clock, legalCase.Id, LegalCaseEventType.NotificationQueued, LegalActorType.System, null,
                new { to = "appellant", subject, body }, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<string?> GetAuthorEmailAsync(Guid reviewId, CancellationToken cancellationToken)
    {
        var review = await reviews.GetByIdAsync(reviewId, cancellationToken);
        if (review is null)
        {
            return null;
        }

        var author = await users.GetByIdAsync(review.UserId, cancellationToken);
        return author is { Status: UserStatus.Active } ? author.Email : null;
    }
}

public sealed record DecideAppealCommand(Guid ActorId, Guid AppealId, string Outcome, string Rationale);

public sealed class DecideAppealCommandHandler(
    ILegalCaseRepository cases,
    IReviewRepository reviews,
    IUserRepository users,
    GymScoreUpdater scoreUpdater,
    IEmailOutbox outbox,
    IUnitOfWork unitOfWork,
    IClock clock) : ICommandHandler<DecideAppealCommand>
{
    public async Task<Result> Handle(DecideAppealCommand command, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<AppealOutcome>(command.Outcome, ignoreCase: true, out var outcome))
        {
            return Result.Failure(Error.Validation("appeal.outcome", "Ungueltiges Ergebnis (DecisionUpheld oder DecisionReversed)."));
        }

        var appeal = await cases.GetAppealByIdAsync(command.AppealId, cancellationToken);
        if (appeal is null)
        {
            return Result.Failure(Error.NotFound("appeal.notFound", "Der Einspruch wurde nicht gefunden."));
        }

        var legalCase = await cases.GetByIdAsync(appeal.LegalCaseId, cancellationToken);
        if (legalCase is null)
        {
            return Result.Failure(Error.NotFound("legalCase.notFound", "Der Fall wurde nicht gefunden."));
        }

        var decideResult = appeal.Decide(outcome, command.Rationale, clock.UtcNow);
        if (decideResult.IsFailure)
        {
            return decideResult;
        }

        var reversed = outcome == AppealOutcome.DecisionReversed;
        if (reversed)
        {
            var review = await reviews.GetByIdAsync(legalCase.ReviewId, cancellationToken);
            if (review is not null)
            {
                var flip = legalCase.Decision == LegalDecision.FullyRemoved
                    ? review.ReinstateFromLegalRemoval(clock.UtcNow)
                    : review.RemoveLegal(clock.UtcNow);
                if (flip.IsFailure)
                {
                    return flip;
                }

                await scoreUpdater.RecalculateAsync(review.GymId, cancellationToken);
            }
        }

        await CaseEvents.AppendAsync(cases, clock, legalCase.Id, LegalCaseEventType.AppealDecided, LegalActorType.Admin, command.ActorId,
            new { appealId = appeal.Id, outcome = outcome.ToString(), rationale = command.Rationale }, cancellationToken);

        var (subject, body) = LegalMailTexts.AppealDecided(legalCase.CaseNumber, reversed);
        outbox.Enqueue(OutboxEmail.Enqueue(legalCase.ReporterEmail, subject, body, "legal.appealDecided", legalCase.Id, clock.UtcNow));
        var authorReview = await reviews.GetByIdAsync(legalCase.ReviewId, cancellationToken);
        if (authorReview is not null)
        {
            var author = await users.GetByIdAsync(authorReview.UserId, cancellationToken);
            if (author is { Status: UserStatus.Active })
            {
                outbox.Enqueue(OutboxEmail.Enqueue(author.Email, subject, body, "legal.appealDecided", legalCase.Id, clock.UtcNow));
            }
        }

        await CaseEvents.AppendAsync(cases, clock, legalCase.Id, LegalCaseEventType.NotificationQueued, LegalActorType.System, null,
            new { to = "parties", subject, body }, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record ApplyLegalHoldCommand(Guid ActorId, string Reason, Guid? LegalCaseId, Guid? ReviewId, Guid? UserId);

public sealed class ApplyLegalHoldCommandHandler(
    ILegalHoldRepository holds,
    ILegalCaseRepository cases,
    IUnitOfWork unitOfWork,
    IClock clock) : ICommandHandler<ApplyLegalHoldCommand, Guid>
{
    public async Task<Result<Guid>> Handle(ApplyLegalHoldCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Reason))
        {
            return Result.Failure<Guid>(Error.Validation("hold.reason", "Eine Begruendung ist erforderlich."));
        }

        if (command.LegalCaseId is null && command.ReviewId is null && command.UserId is null)
        {
            return Result.Failure<Guid>(Error.Validation("hold.target", "Ein Ziel (Fall, Bewertung oder Konto) ist erforderlich."));
        }

        var hold = LegalHold.Create(command.Reason, command.LegalCaseId, command.ReviewId, command.UserId, clock.UtcNow);
        holds.Add(hold);

        if (command.LegalCaseId is Guid caseId && await cases.GetByIdAsync(caseId, cancellationToken) is not null)
        {
            await CaseEvents.AppendAsync(cases, clock, caseId, LegalCaseEventType.LegalHoldApplied, LegalActorType.Admin, command.ActorId,
                new { holdId = hold.Id, reason = command.Reason }, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return hold.Id;
    }
}

public sealed record ReleaseLegalHoldCommand(Guid ActorId, Guid HoldId);

public sealed class ReleaseLegalHoldCommandHandler(
    ILegalHoldRepository holds,
    ILegalCaseRepository cases,
    IUnitOfWork unitOfWork,
    IClock clock) : ICommandHandler<ReleaseLegalHoldCommand>
{
    public async Task<Result> Handle(ReleaseLegalHoldCommand command, CancellationToken cancellationToken)
    {
        var hold = await holds.GetByIdAsync(command.HoldId, cancellationToken);
        if (hold is null)
        {
            return Result.Failure(Error.NotFound("hold.notFound", "Der Legal Hold wurde nicht gefunden."));
        }

        hold.Release(clock.UtcNow);

        if (hold.LegalCaseId is Guid caseId && await cases.GetByIdAsync(caseId, cancellationToken) is not null)
        {
            await CaseEvents.AppendAsync(cases, clock, caseId, LegalCaseEventType.LegalHoldReleased, LegalActorType.Admin, command.ActorId,
                new { holdId = hold.Id }, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
