using System.Globalization;
using System.Text;

namespace Gym.Domain.Common;

public abstract class Entity
{
    public Guid Id { get; protected set; }
}

public static class Slug
{
    /// <summary>Generates a stable URL slug (lowercase ASCII, hyphen separated, German transliteration).</summary>
    public static string Generate(string input)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        var normalized = input.Trim().ToLowerInvariant()
            .Replace("ä", "ae", StringComparison.Ordinal)
            .Replace("ö", "oe", StringComparison.Ordinal)
            .Replace("ü", "ue", StringComparison.Ordinal)
            .Replace("ß", "ss", StringComparison.Ordinal);

        // Strip remaining diacritics.
        var formD = normalized.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(formD.Length);
        foreach (var c in formD)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(c);
        }

        var ascii = builder.ToString().Normalize(NormalizationForm.FormC);
        var slug = new StringBuilder(ascii.Length);
        var lastWasHyphen = true;
        foreach (var c in ascii)
        {
            if (c is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                slug.Append(c);
                lastWasHyphen = false;
            }
            else if (!lastWasHyphen)
            {
                slug.Append('-');
                lastWasHyphen = true;
            }
        }

        var result = slug.ToString().Trim('-');
        return result.Length == 0 ? "n-a" : result;
    }
}

public static class TextSanitizer
{
    /// <summary>Trims, normalizes newlines and removes control characters except line breaks.</summary>
    public static string? Sanitize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var normalized = input.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        var builder = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (c == '\n' || !char.IsControl(c))
            {
                builder.Append(c);
            }
        }

        var result = builder.ToString();
        return result.Length == 0 ? null : result;
    }
}
