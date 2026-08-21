using Gym.Application.Abstractions;
using Gym.Application.Contracts;
using Gym.Domain.Common;
using Gym.Domain.Entities;
using Gym.Domain.Enums;

namespace Gym.Application.Features.Legal;

public sealed record GetActiveLegalDocumentQuery(string Type);

public sealed class GetActiveLegalDocumentQueryHandler(ILegalDocumentRepository documents) : IQueryHandler<GetActiveLegalDocumentQuery, LegalDocumentDto>
{
    public async Task<Result<LegalDocumentDto>> Handle(GetActiveLegalDocumentQuery query, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<LegalDocumentType>(query.Type, ignoreCase: true, out var type))
        {
            return Result.Failure<LegalDocumentDto>(Error.Validation("legalDocument.type", "Ungueltiger Dokumenttyp."));
        }

        var document = await documents.GetActiveAsync(type, cancellationToken);
        if (document is null)
        {
            return Result.Failure<LegalDocumentDto>(Error.NotFound("legalDocument.notFound", "Das Dokument ist noch nicht veroeffentlicht."));
        }

        return ToDto(document);
    }

    internal static LegalDocumentDto ToDto(LegalDocument d) => new(
        d.Id, d.Type.ToString(), d.Version, d.Title, d.ContentMarkdown, d.IsPublished, d.CreatedAtUtc, d.PublishedAtUtc);
}

public sealed record ListLegalDocumentVersionsQuery(string Type);

public sealed class ListLegalDocumentVersionsQueryHandler(ILegalDocumentRepository documents) : IQueryHandler<ListLegalDocumentVersionsQuery, IReadOnlyList<LegalDocumentDto>>
{
    public async Task<Result<IReadOnlyList<LegalDocumentDto>>> Handle(ListLegalDocumentVersionsQuery query, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<LegalDocumentType>(query.Type, ignoreCase: true, out var type))
        {
            return Result.Failure<IReadOnlyList<LegalDocumentDto>>(Error.Validation("legalDocument.type", "Ungueltiger Dokumenttyp."));
        }

        var versions = await documents.ListVersionsAsync(type, cancellationToken);
        return Result.Success<IReadOnlyList<LegalDocumentDto>>(
            versions.Select(GetActiveLegalDocumentQueryHandler.ToDto).ToList());
    }
}

public sealed record CreateLegalDocumentVersionCommand(string Type, string Title, string ContentMarkdown);

public sealed class CreateLegalDocumentVersionCommandHandler(
    ILegalDocumentRepository documents,
    IUnitOfWork unitOfWork,
    IClock clock) : ICommandHandler<CreateLegalDocumentVersionCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateLegalDocumentVersionCommand command, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<LegalDocumentType>(command.Type, ignoreCase: true, out var type))
        {
            return Result.Failure<Guid>(Error.Validation("legalDocument.type", "Ungueltiger Dokumenttyp."));
        }

        if (string.IsNullOrWhiteSpace(command.Title) || string.IsNullOrWhiteSpace(command.ContentMarkdown))
        {
            return Result.Failure<Guid>(Error.Validation("legalDocument.content", "Titel und Inhalt sind erforderlich."));
        }

        var nextVersion = await documents.GetMaxVersionAsync(type, cancellationToken) + 1;
        var document = LegalDocument.CreateDraft(type, nextVersion, command.Title, command.ContentMarkdown, clock.UtcNow);
        documents.Add(document);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return document.Id;
    }
}

public sealed record PublishLegalDocumentCommand(Guid DocumentId);

public sealed class PublishLegalDocumentCommandHandler(
    ILegalDocumentRepository documents,
    IUnitOfWork unitOfWork,
    IClock clock) : ICommandHandler<PublishLegalDocumentCommand>
{
    public async Task<Result> Handle(PublishLegalDocumentCommand command, CancellationToken cancellationToken)
    {
        var document = await documents.GetByIdAsync(command.DocumentId, cancellationToken);
        if (document is null)
        {
            return Result.Failure(Error.NotFound("legalDocument.notFound", "Das Dokument wurde nicht gefunden."));
        }

        document.Publish(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
