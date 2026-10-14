using Gym.Domain.Common;
using Gym.Domain.Enums;
using Gym.Infrastructure.Seeding;
using Xunit;

namespace Gym.IntegrationTests;

public sealed class EmbeddedSeedDataTests
{
    [Fact]
    public void Embedded_seed_resources_load_expected_active_data_shape()
    {
        Assert.NotEmpty(SeedVocabulary.Chains);
        Assert.NotEmpty(SeedVocabulary.Amenities);
        Assert.NotEmpty(ViennaCatalog.Gyms);
        Assert.Equal(
            SeedVocabulary.Chains.Count,
            SeedVocabulary.Chains.Select(chain => Slug.Generate(chain.Name)).Distinct().Count());
        var chainSlugs = SeedVocabulary.Chains
            .Select(chain => Slug.Generate(chain.Name))
            .ToHashSet(StringComparer.Ordinal);
        Assert.All(
            ViennaCatalog.Gyms.Where(gym => gym.ChainSlug is not null),
            gym => Assert.Contains(gym.ChainSlug!, chainSlugs));
        Assert.True(ViennaCatalog.Gyms.Single(gym => gym.Name == "FitInn Wien Meiselmarkt").TemporarilyClosed);
        Assert.Equal(3, LegalDocumentDrafts.All.Count);
        Assert.Equal(3, EmbeddedSeedData.Demo.Users!.Count);
        Assert.Equal(3, EmbeddedSeedData.Demo.Ratings!.Count);
        Assert.Equal(LegalCaseCategory.Other, EmbeddedSeedData.Demo.LegalCase!.Category);
    }
}
