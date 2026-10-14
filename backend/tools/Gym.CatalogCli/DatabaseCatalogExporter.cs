using Gym.Domain.Entities;
using Gym.Domain.Enums;
using Gym.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Gym.CatalogCli;

internal sealed class DatabaseCatalogExporter(CatalogFile catalogFile)
{
    public async Task ExportAsync(
        string connectionString,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var context = new AppDbContext(options);
        var chains = await context.GymChains.AsNoTracking().ToListAsync(cancellationToken);
        var gyms = await context.Gyms.AsNoTracking().ToListAsync(cancellationToken);
        var amenities = await context.Amenities.AsNoTracking().ToListAsync(cancellationToken);

        catalogFile.Save(outputPath, CreateDocument(chains, gyms, amenities));
    }

    internal static CatalogDocument CreateDocument(
        IEnumerable<GymChain> chains,
        IEnumerable<GymEntry> gyms,
        IEnumerable<Amenity> amenities)
    {
        var orderedChains = chains.OrderBy(chain => chain.Name, StringComparer.Ordinal).ToArray();
        var chainSlugs = orderedChains.ToDictionary(chain => chain.Id, chain => chain.Slug);

        return new CatalogDocument
        {
            Chains = orderedChains
                .Select(chain => new CatalogChain
                {
                    Name = chain.Name,
                    Website = chain.Website,
                    TrainingTypes = ExpandTrainingTypes(chain.TrainingTypes),
                    TargetAudience = chain.TargetAudience,
                })
                .ToList(),
            Amenities = amenities
                .Select(amenity => amenity.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList(),
            ViennaGyms = gyms
                .OrderBy(gym => gym.District)
                .ThenBy(gym => gym.Name, StringComparer.Ordinal)
                .Select(gym => new CatalogGym
                {
                    Name = gym.Name,
                    ChainSlug = gym.ChainId is { } chainId
                        && chainSlugs.TryGetValue(chainId, out var chainSlug)
                            ? chainSlug
                            : null,
                    District = gym.District,
                    Address = gym.AddressLine,
                    PostalCode = gym.PostalCode,
                    Status = gym.Status,
                    TrainingTypes = gym.TrainingTypesOverride is { } trainingTypes
                        ? ExpandTrainingTypes(trainingTypes)
                        : null,
                    TargetAudience = gym.TargetAudienceOverride,
                })
                .ToList(),
            RegionalCatalogs = new Dictionary<string, List<RegionalCatalogGym>>(StringComparer.Ordinal),
        };
    }

    private static List<TrainingType> ExpandTrainingTypes(TrainingType trainingTypes) =>
        Enum.GetValues<TrainingType>()
            .Where(value => value != TrainingType.None && trainingTypes.HasFlag(value))
            .ToList();
}
