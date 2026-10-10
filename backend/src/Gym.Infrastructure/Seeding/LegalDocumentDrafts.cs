namespace Gym.Infrastructure.Seeding;

internal static class LegalDocumentDrafts
{
    public static readonly IReadOnlyList<LegalDocumentSeed> All =
        EmbeddedSeedData.Require(EmbeddedSeedData.LegalDocuments.Documents, "legalDocuments.documents");
}
