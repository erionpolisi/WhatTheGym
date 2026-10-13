namespace Gym.CatalogCli;

internal readonly record struct VerificationSummary(int UnverifiedCount, int StaleCount);

internal static class VerificationStatus
{
    public static bool IsStale(DateOnly verifiedOn, DateOnly today) =>
        verifiedOn < today.AddMonths(-1);
}

internal sealed class TerminalFormatter(bool useColors)
{
    private const string RedCode = "\u001b[31m";
    private const string OrangeCode = "\u001b[38;5;208m";
    private const string ResetCode = "\u001b[0m";

    public string Red(string value) => Color(value, RedCode);

    public string Orange(string value) => Color(value, OrangeCode);

    public string VerificationCounts(VerificationSummary summary)
    {
        var counts = new List<string>(capacity: 2);
        if (summary.UnverifiedCount > 0)
        {
            counts.Add(Red($"({summary.UnverifiedCount})"));
        }

        if (summary.StaleCount > 0)
        {
            counts.Add(Orange($"({summary.StaleCount})"));
        }

        return counts.Count == 0 ? string.Empty : $" | {string.Join(" | ", counts)}";
    }

    private string Color(string value, string colorCode) =>
        useColors ? $"{colorCode}{value}{ResetCode}" : value;
}
