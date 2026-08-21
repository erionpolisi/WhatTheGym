using FluentValidation.Results;
using Gym.Domain.Common;

namespace Gym.Application.Common;

public static class ValidationExtensions
{
    public static Error ToError(this ValidationResult result) =>
        Error.Validation("validation", string.Join(" ", result.Errors.Select(e => e.ErrorMessage).Distinct()));
}

public static class SlugUniquifier
{
    /// <summary>Appends -2, -3, ... until the slug is unique. Slugs are stable afterwards.</summary>
    public static async Task<string> EnsureUniqueAsync(string baseSlug, Func<string, Task<bool>> existsAsync)
    {
        var candidate = baseSlug;
        var suffix = 2;
        while (await existsAsync(candidate).ConfigureAwait(false))
        {
            candidate = $"{baseSlug}-{suffix}";
            suffix++;
        }

        return candidate;
    }
}
