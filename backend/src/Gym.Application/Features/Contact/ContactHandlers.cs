using FluentValidation;
using Gym.Application.Abstractions;
using Gym.Application.Common;
using Gym.Application.Contracts;
using Gym.Application.Features.Legal;
using Gym.Domain.Common;
using Gym.Domain.Entities;
using Gym.Domain.Enums;

namespace Gym.Application.Features.Contact;

public sealed record CreateContactRequestCommand(
    string Type,
    string Name,
    string Email,
    string Message,
    string? GymSlug);

public sealed class CreateContactRequestCommandValidator : AbstractValidator<CreateContactRequestCommand>
{
    public CreateContactRequestCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(120).WithMessage("Name ist erforderlich (max. 120 Zeichen).");
        RuleFor(c => c.Email).NotEmpty().EmailAddress().MaximumLength(254).WithMessage("Eine gueltige E-Mail-Adresse ist erforderlich.");
        RuleFor(c => c.Message).NotEmpty().MinimumLength(10).MaximumLength(ContactRequest.MaxMessageLength)
            .WithMessage($"Die Nachricht muss zwischen 10 und {ContactRequest.MaxMessageLength} Zeichen lang sein.");
        RuleFor(c => c.Message).Must(m => m is null || System.Text.RegularExpressions.Regex.Count(m, "https?://") <= 3)
            .WithMessage("Die Nachricht enthaelt zu viele Links.");
    }
}

public sealed class CreateContactRequestCommandHandler(
    IContactRequestRepository contacts,
    IGymRepository gyms,
    IEmailOutbox outbox,
    IUnitOfWork unitOfWork,
    IClock clock,
    IValidator<CreateContactRequestCommand> validator) : ICommandHandler<CreateContactRequestCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateContactRequestCommand command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<Guid>(validation.ToError());
        }

        if (!Enum.TryParse<ContactRequestType>(command.Type, ignoreCase: true, out var type))
        {
            return Result.Failure<Guid>(Error.Validation("contact.type", "Ungueltiger Anfragetyp."));
        }

        Guid? gymId = null;
        if (!string.IsNullOrWhiteSpace(command.GymSlug))
        {
            var gym = await gyms.GetBySlugAsync(command.GymSlug, cancellationToken);
            if (gym is null)
            {
                return Result.Failure<Guid>(Error.NotFound("gym.notFound", "Das angegebene Studio wurde nicht gefunden."));
            }

            gymId = gym.Id;
        }

        var requestResult = ContactRequest.Create(type, command.Name, command.Email, command.Message, gymId, clock.UtcNow);
        if (requestResult.IsFailure)
        {
            return Result.Failure<Guid>(requestResult.Error);
        }

        contacts.Add(requestResult.Value);

        var (subject, body) = LegalMailTexts.ContactConfirmation(requestResult.Value.Name);
        outbox.Enqueue(OutboxEmail.Enqueue(requestResult.Value.Email, subject, body, "contact.confirmation", null, clock.UtcNow));

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return requestResult.Value.Id;
    }
}

public sealed record ListContactRequestsQuery(string? Status, int? Page, int? PageSize);

public sealed class ListContactRequestsQueryHandler(IContactRequestRepository contacts) : IQueryHandler<ListContactRequestsQuery, PagedResult<ContactRequestDto>>
{
    public async Task<Result<PagedResult<ContactRequestDto>>> Handle(ListContactRequestsQuery query, CancellationToken cancellationToken)
    {
        ContactRequestStatus? status = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!Enum.TryParse<ContactRequestStatus>(query.Status, ignoreCase: true, out var parsed))
            {
                return Result.Failure<PagedResult<ContactRequestDto>>(Error.Validation("contact.status", "Ungueltiger Status."));
            }

            status = parsed;
        }

        var (page, pageSize) = Paging.Normalize(query.Page, query.PageSize);
        var result = await contacts.ListAsync(status, page, pageSize, cancellationToken);
        var items = result.Items
            .Select(c => new ContactRequestDto(
                c.Id, c.Type.ToString(), c.Name, c.Email, c.Message, c.GymId, c.Status.ToString(), c.CreatedAtUtc))
            .ToList();
        return new PagedResult<ContactRequestDto>(items, result.Page, result.PageSize, result.TotalCount);
    }
}

public sealed record SetContactRequestStatusCommand(Guid RequestId, string Status);

public sealed class SetContactRequestStatusCommandHandler(
    IContactRequestRepository contacts,
    IUnitOfWork unitOfWork,
    IClock clock) : ICommandHandler<SetContactRequestStatusCommand>
{
    public async Task<Result> Handle(SetContactRequestStatusCommand command, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<ContactRequestStatus>(command.Status, ignoreCase: true, out var status))
        {
            return Result.Failure(Error.Validation("contact.status", "Ungueltiger Status."));
        }

        var request = await contacts.GetByIdAsync(command.RequestId, cancellationToken);
        if (request is null)
        {
            return Result.Failure(Error.NotFound("contact.notFound", "Die Anfrage wurde nicht gefunden."));
        }

        request.SetStatus(status, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
