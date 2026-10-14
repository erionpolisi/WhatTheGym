using System.Text;
using Gym.CatalogCli;

Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = Encoding.UTF8;

try
{
    if (args.FirstOrDefault()?.Equals("export-db", StringComparison.OrdinalIgnoreCase) == true)
    {
        var sourceCatalogPath = CatalogPathResolver.Resolve(args.ElementAtOrDefault(1));
        var outputPath = Path.Combine(Path.GetDirectoryName(sourceCatalogPath)!, "catalog-DB.json");
        var connectionString = DatabaseConnectionResolver.Resolve();
        await new DatabaseCatalogExporter(new CatalogFile())
            .ExportAsync(connectionString, outputPath, CancellationToken.None);
        Console.WriteLine($"Datenbankkatalog exportiert: {outputPath}");
        return 0;
    }

    var catalogPath = CatalogPathResolver.Resolve(args.FirstOrDefault());
    var application = new ConsoleCatalogApplication(
        catalogPath,
        new CatalogFile(),
        new BrowserLauncher(),
        Console.In,
        Console.Out,
        () => DateOnly.FromDateTime(DateTime.Today),
        new TerminalFormatter(useColors: !Console.IsOutputRedirected),
        ClearScreen);
    application.Run();
    return 0;
}
catch (Exception exception) when (
    exception is IOException
        or UnauthorizedAccessException
        or InvalidOperationException
        or System.Data.Common.DbException)
{
    Console.Error.WriteLine($"Fehler: {exception.Message}");
    return 1;
}

static void ClearScreen()
{
    if (!Console.IsOutputRedirected)
    {
        Console.Clear();
        Console.Write("\u001b[3J\u001b[H");
        Console.Out.Flush();
    }
}

internal static class DatabaseConnectionResolver
{
    public static string Resolve()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings:Postgres");
        return !string.IsNullOrWhiteSpace(connectionString)
            ? connectionString
            : throw new InvalidOperationException(
                "Für export-db muss ConnectionStrings__Postgres gesetzt sein.");
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
