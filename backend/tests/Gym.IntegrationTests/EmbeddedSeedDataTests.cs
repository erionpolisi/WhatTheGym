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
        Assert.Equal(23, SeedVocabulary.Chains.Count);
        Assert.Equal(113, ViennaCatalog.Gyms.Count);
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
        Assert.All(SeedVocabulary.ChainSeeds, chain => Assert.NotEmpty(chain.TrainingTypes!));
        Assert.Equal(
            TargetAudience.WomenOnly,
            SeedVocabulary.ChainSeeds.Single(chain => chain.Name == "Mrs.Sporty").TargetAudience);
        Assert.Contains(ViennaCatalog.Gyms, gym => gym.Name == "Fitness First Wien-Liesing");
        Assert.Contains(ViennaCatalog.Gyms, gym => gym.Name == "Anytime Fitness WIEN FABRIK 1230");
        Assert.Equal(3, LegalDocumentDrafts.All.Count);
        Assert.Equal(3, EmbeddedSeedData.Demo.Users!.Count);
        Assert.Equal(3, EmbeddedSeedData.Demo.Ratings!.Count);
        Assert.Equal(LegalCaseCategory.Other, EmbeddedSeedData.Demo.LegalCase!.Category);
    }
}
