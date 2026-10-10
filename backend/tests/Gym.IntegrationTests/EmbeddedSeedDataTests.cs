using Gym.Domain.Enums;
using Gym.Infrastructure.Seeding;
using Xunit;

namespace Gym.IntegrationTests;

public sealed class EmbeddedSeedDataTests
{
    [Fact]
    public void Embedded_seed_resources_load_expected_active_data_shape()
    {
        Assert.Equal(11, SeedVocabulary.Chains.Count);
        Assert.Equal(12, SeedVocabulary.Amenities.Count);
        Assert.Equal(92, ViennaCatalog.Gyms.Count);
        Assert.Equal(3, LegalDocumentDrafts.All.Count);
        Assert.Equal(3, EmbeddedSeedData.Demo.Users!.Count);
        Assert.Equal(3, EmbeddedSeedData.Demo.Ratings!.Count);
        Assert.Equal(LegalCaseCategory.Other, EmbeddedSeedData.Demo.LegalCase!.Category);
    }
}
