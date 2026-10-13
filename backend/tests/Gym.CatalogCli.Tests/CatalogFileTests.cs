using Gym.CatalogCli;
using Xunit;

namespace Gym.CatalogCli.Tests;

public sealed class CatalogFileTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"gym-catalog-cli-{Guid.NewGuid():N}");

    [Fact]
    public void Save_and_load_preserve_catalog_and_verification_dates()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "catalog.json");
        var catalog = CreateCatalog();
        catalog.Chains[0].WebsiteVerifiedOn = new DateOnly(2026, 10, 13);
        catalog.ViennaGyms[0].LocationVerifiedOn = new DateOnly(2026, 10, 12);
        var file = new CatalogFile();

        file.Save(path, catalog);
        var loaded = file.Load(path);

        Assert.Equal(new DateOnly(2026, 10, 13), loaded.Chains[0].WebsiteVerifiedOn);
        Assert.Equal(new DateOnly(2026, 10, 12), loaded.ViennaGyms[0].LocationVerifiedOn);
        Assert.Equal("Regional Gym", loaded.RegionalCatalogs["Burgenland"][0].Name);
    }

    [Fact]
    public void Editing_an_address_clears_location_verification()
    {
        var catalog = CreateCatalog();
        var gym = catalog.ViennaGyms[0];
        gym.LocationVerifiedOn = new DateOnly(2026, 10, 13);
        var service = new CatalogService(catalog);

        service.UpdateGym(gym, gym.Name, 2, "Neue Straße 2", "1020");

        Assert.Null(gym.LocationVerifiedOn);
    }

    [Fact]
    public void Renaming_a_chain_updates_location_references()
    {
        var catalog = CreateCatalog();
        var service = new CatalogService(catalog);

        service.UpdateChain(catalog.Chains[0], "Example Fitness", "https://example.com");

        Assert.Equal("example-fitness", catalog.ViennaGyms[0].ChainSlug);
    }

    [Fact]
    public void Google_maps_url_contains_encoded_vienna_address()
    {
        var gym = CreateCatalog().ViennaGyms[0];

        var url = CatalogService.CreateGoogleMapsUrl(gym);

        Assert.Equal(
            "https://www.google.com/maps/search/?api=1&query=Teststra%C3%9Fe%201%2C%201020%20Wien",
            url);
    }

    [Fact]
    public void Validation_rejects_unknown_chain_reference()
    {
        var catalog = CreateCatalog();
        catalog.ViennaGyms[0].ChainSlug = "unknown";

        var exception = Assert.Throws<InvalidOperationException>(() => CatalogFile.Validate(catalog));

        Assert.Contains("unbekannte Kette", exception.Message);
    }

    [Fact]
    public void Location_verification_summary_separates_unverified_and_stale_locations()
    {
        var catalog = CreateCatalog();
        catalog.ViennaGyms.Add(
            new CatalogGym
            {
                Name = "Example Gym Wien Mitte",
                ChainSlug = "example-gym",
                District = 3,
                Address = "Testgasse 2",
                PostalCode = "1030",
                LocationVerifiedOn = new DateOnly(2026, 9, 12),
            });
        catalog.ViennaGyms.Add(
            new CatalogGym
            {
                Name = "Example Gym Wien West",
                ChainSlug = "example-gym",
                District = 15,
                Address = "Testweg 3",
                PostalCode = "1150",
                LocationVerifiedOn = new DateOnly(2026, 9, 13),
            });
        var service = new CatalogService(catalog);

        var summary = service.GetLocationVerificationSummary(
            catalog.Chains[0],
            new DateOnly(2026, 10, 13));

        Assert.Equal(new VerificationSummary(UnverifiedCount: 1, StaleCount: 1), summary);
    }

    [Fact]
    public void Terminal_formatter_applies_requested_colors_when_enabled()
    {
        var formatter = new TerminalFormatter(useColors: true);

        Assert.Equal("\u001b[31m(2)\u001b[0m", formatter.Red("(2)"));
        Assert.Equal("\u001b[38;5;208m2026-09-12\u001b[0m", formatter.Orange("2026-09-12"));
        Assert.Equal(
            " | \u001b[31m(2)\u001b[0m",
            formatter.VerificationCounts(new VerificationSummary(UnverifiedCount: 2, StaleCount: 0)));
        Assert.Equal(
            string.Empty,
            formatter.VerificationCounts(new VerificationSummary(UnverifiedCount: 0, StaleCount: 0)));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static CatalogDocument CreateCatalog() => new()
    {
        Chains =
        [
            new CatalogChain { Name = "Example Gym", Website = "https://example.com" },
        ],
        Amenities = ["Duschen"],
        ViennaGyms =
        [
            new CatalogGym
            {
                Name = "Example Gym Wien",
                ChainSlug = "example-gym",
                District = 2,
                Address = "Teststraße 1",
                PostalCode = "1020",
            },
        ],
        RegionalCatalogs = new Dictionary<string, List<RegionalCatalogGym>>(StringComparer.Ordinal)
        {
            ["Burgenland"] =
            [
                new RegionalCatalogGym
                {
                    Name = "Regional Gym",
                    ChainSlug = "example-gym",
                    City = "Eisenstadt",
                    PostalCode = "7000",
                    Address = "Hauptstraße 1",
                    Website = null,
                },
            ],
        },
    };
}
