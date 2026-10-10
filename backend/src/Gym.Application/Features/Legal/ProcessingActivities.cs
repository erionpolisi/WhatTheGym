using System.Text.Json;

namespace Gym.Application.Features.Legal;

public sealed record ProcessingActivityDto(
    string Name,
    string Purpose,
    string LegalBasis,
    IReadOnlyList<string> DataCategories,
    IReadOnlyList<string> Entities,
    string Retention,
    IReadOnlyList<string> Recipients);

/// <summary>
/// Record of processing activities (GDPR Art. 30). Every entity that stores values logically
/// linkable to a person must be listed here; a test enforces the coverage.
/// Marked as draft: ENTWURF - anwaltlich prüfen lassen.
/// </summary>
public static class ProcessingActivitiesRecord
{
    private static readonly ProcessingActivitiesSeed Data = Load();

    public static readonly string Version = Require(Data.Version, "version");

    public static readonly IReadOnlyList<ProcessingActivityDto> Activities =
        Require(Data.Activities, "activities");

    /// <summary>Entities that persist personal data; used by tests to enforce record coverage.</summary>
    public static readonly IReadOnlyList<string> PersonalDataEntities =
        Require(Data.PersonalDataEntities, "personalDataEntities");

    private static ProcessingActivitiesSeed Load()
    {
        const string resourceName = "Gym.Application.Features.Legal.Data.processing-activities.json";
        using var stream = typeof(ProcessingActivitiesRecord).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded processing-activities resource '{resourceName}' was not found.");

        try
        {
            return JsonSerializer.Deserialize<ProcessingActivitiesSeed>(stream)
                ?? throw new InvalidOperationException($"Embedded processing-activities resource '{resourceName}' contains null data.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"Embedded processing-activities resource '{resourceName}' is malformed.", exception);
        }
    }

    private static T Require<T>(T? value, string dataName) where T : class =>
        value ?? throw new InvalidOperationException($"Embedded processing-activities data '{dataName}' is missing or null.");
}

internal sealed class ProcessingActivitiesSeed
{
    public string? Version { get; init; }

    public IReadOnlyList<ProcessingActivityDto>? Activities { get; init; }

    public IReadOnlyList<string>? PersonalDataEntities { get; init; }
}
