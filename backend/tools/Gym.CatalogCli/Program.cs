using System.Text;
using Gym.CatalogCli;

Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = Encoding.UTF8;

try
{
    var catalogPath = CatalogPathResolver.Resolve(args.FirstOrDefault());
    var application = new ConsoleCatalogApplication(
        catalogPath,
        new CatalogFile(),
        new BrowserLauncher(),
        Console.In,
        Console.Out,
        () => DateOnly.FromDateTime(DateTime.Today),
        ClearScreen);
    application.Run();
    return 0;
}
catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
{
    Console.Error.WriteLine($"Fehler: {exception.Message}");
    return 1;
}

static void ClearScreen()
{
    if (!Console.IsOutputRedirected)
    {
        Console.Clear();
    }
}

internal static class CatalogPathResolver
{
    public static string Resolve(string? explicitPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            var fullPath = Path.GetFullPath(explicitPath);
            return File.Exists(fullPath)
                ? fullPath
                : throw new InvalidOperationException($"Katalog nicht gefunden: {fullPath}");
        }

        var fromWorkingDirectory = SearchParents(Directory.GetCurrentDirectory());
        if (fromWorkingDirectory is not null)
        {
            return fromWorkingDirectory;
        }

        var fromApplicationDirectory = SearchParents(AppContext.BaseDirectory);
        return fromApplicationDirectory
            ?? throw new InvalidOperationException(
                "catalog.json wurde nicht gefunden. Übergib den Pfad als erstes Argument.");
    }

    private static string? SearchParents(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src",
                "Gym.Infrastructure",
                "Seeding",
                "Data",
                "catalog.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
