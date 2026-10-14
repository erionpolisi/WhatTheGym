using System.Globalization;

namespace Gym.CatalogCli;

internal sealed class ConsoleCatalogApplication(
    string catalogPath,
    CatalogFile catalogFile,
    IBrowserLauncher browser,
    TextReader input,
    TextWriter output,
    Func<DateOnly> today,
    TerminalFormatter terminal,
    Action clearScreen)
{
    private readonly CatalogService _service = new(catalogFile.Load(catalogPath));

    public void Run()
    {
        output.WriteLine($"WhatTheGym Katalog: {catalogPath}");

        while (true)
        {
            var currentDate = today();
            var chains = _service.Catalog.Chains
                .OrderBy(chain => chain.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            output.WriteLine();
            output.WriteLine("Ketten");
            output.WriteLine($"{terminal.Red("Rot")}: nicht verifiziert | {terminal.Orange("Orange")}: älter als ein Monat");
            for (var index = 0; index < chains.Length; index++)
            {
                var listedChain = chains[index];
                var summary = _service.GetLocationVerificationSummary(listedChain, currentDate);
                output.WriteLine(
                    $"{index + 1,2}. {listedChain.Name}{FormatChainTags(listedChain)}"
                    + $" | {listedChain.Website ?? "-"} | geprüft: {FormatDate(listedChain.WebsiteVerifiedOn, currentDate)}"
                    + terminal.VerificationCounts(summary));
            }

            output.WriteLine();
            output.WriteLine("[Nummer] öffnen | [N] neue Kette | [Q] beenden");
            var command = ReadCommand();
            if (command.Equals("q", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (command.Equals("n", StringComparison.OrdinalIgnoreCase))
            {
                AddChain();
                continue;
            }

            if (TrySelect(command, chains, out var chain))
            {
                ShowChain(chain);
            }
            else
            {
                output.WriteLine("Ungültige Auswahl.");
            }
        }
    }

    private void ShowChain(CatalogChain chain)
    {
        while (true)
        {
            var currentDate = today();
            var gyms = _service.GetGyms(chain);
            var summary = _service.GetLocationVerificationSummary(chain, currentDate);
            output.WriteLine();
            output.WriteLine(
                $"{chain.Name}{FormatChainTags(chain)}"
                + $" | {chain.Website ?? "-"} | Website geprüft: {FormatDate(chain.WebsiteVerifiedOn, currentDate)}"
                + terminal.VerificationCounts(summary));
            output.WriteLine("Standorte");

            if (gyms.Count == 0)
            {
                output.WriteLine("  Keine Wiener Standorte.");
            }
            else
            {
                for (var index = 0; index < gyms.Count; index++)
                {
                    var gym = gyms[index];
                    output.WriteLine(
                        $"{index + 1,2}. {gym.Name} | {gym.Address}, {gym.PostalCode} Wien"
                        + $" | geprüft: {FormatDate(gym.LocationVerifiedOn, currentDate)}"
                        + FormatClosureStatus(gym));
                }
            }

            output.WriteLine();
            output.WriteLine("[Nummer] Standort/Maps | [W] Website | [V] Website verifizieren");
            output.WriteLine("[E] Kette bearbeiten | [N] neuer Standort | [B] zurück");

            var command = ReadCommand();
            if (command.Equals("b", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (command.Equals("w", StringComparison.OrdinalIgnoreCase))
            {
                OpenWebsite(chain);
            }
            else if (command.Equals("v", StringComparison.OrdinalIgnoreCase))
            {
                if (chain.Website is null)
                {
                    output.WriteLine("Ohne hinterlegte Website kann die Kette nicht verifiziert werden.");
                    continue;
                }

                chain.WebsiteVerifiedOn = ReadVerificationDate();
                Save();
            }
            else if (command.Equals("e", StringComparison.OrdinalIgnoreCase))
            {
                EditChain(chain);
            }
            else if (command.Equals("n", StringComparison.OrdinalIgnoreCase))
            {
                AddGym(chain);
            }
            else if (TrySelect(command, gyms, out var gym))
            {
                ShowGym(gym);
            }
            else
            {
                output.WriteLine("Ungültige Auswahl.");
            }
        }
    }

    private void ShowGym(CatalogGym gym)
    {
        OpenUrl(CatalogService.CreateGoogleMapsUrl(gym), "Google Maps");

        while (true)
        {
            var currentDate = today();
            output.WriteLine();
            output.WriteLine($"{gym.Name}");
            output.WriteLine($"{gym.Address}, {gym.PostalCode} Wien ({gym.District}. Bezirk)");
            output.WriteLine($"Standort geprüft: {FormatDate(gym.LocationVerifiedOn, currentDate)}");
            output.WriteLine($"Status: {(gym.TemporarilyClosed == true ? terminal.Orange("vorübergehend geschlossen") : "aktiv")}");
            output.WriteLine("[M] Maps erneut öffnen | [V] Standort verifizieren | [S] Status ändern");
            output.WriteLine("[E] bearbeiten | [B] zurück");

            var command = ReadCommand();
            if (command.Equals("b", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (command.Equals("m", StringComparison.OrdinalIgnoreCase))
            {
                OpenUrl(CatalogService.CreateGoogleMapsUrl(gym), "Google Maps");
            }
            else if (command.Equals("v", StringComparison.OrdinalIgnoreCase))
            {
                gym.LocationVerifiedOn = ReadVerificationDate();
                Save();
                return;
            }
            else if (command.Equals("e", StringComparison.OrdinalIgnoreCase))
            {
                EditGym(gym);
            }
            else if (command.Equals("s", StringComparison.OrdinalIgnoreCase))
            {
                CatalogService.ToggleTemporaryClosure(gym);
                Save();
                return;
            }
            else
            {
                output.WriteLine("Ungültige Auswahl.");
            }
        }
    }

    private void AddChain()
    {
        output.WriteLine();
        var name = ReadRequired("Name");
        var website = ReadOptional("Website (optional)");

        ExecuteAndSave(() => _service.AddChain(name, website));
    }

    private void EditChain(CatalogChain chain)
    {
        output.WriteLine();
        output.WriteLine("Enter behält den aktuellen Wert; '-' entfernt die Website.");
        var name = ReadWithDefault("Name", chain.Name);
        var websiteInput = ReadWithDefault("Website", chain.Website ?? "-");
        var website = websiteInput == "-" ? null : websiteInput;

        ExecuteAndSave(() => _service.UpdateChain(chain, name, website));
    }

    private void AddGym(CatalogChain chain)
    {
        output.WriteLine();
        var name = ReadRequired("Name");
        var district = ReadDistrict(null);
        var address = ReadRequired("Adresse");
        var postalCode = ReadWithDefault(
            "Postleitzahl",
            (1000 + (district * 10)).ToString(CultureInfo.InvariantCulture));

        ExecuteAndSave(() => _service.AddGym(chain, name, district, address, postalCode));
    }

    private void EditGym(CatalogGym gym)
    {
        output.WriteLine();
        output.WriteLine("Enter behält den aktuellen Wert.");
        var name = ReadWithDefault("Name", gym.Name);
        var district = ReadDistrict(gym.District);
        var address = ReadWithDefault("Adresse", gym.Address);
        var postalCode = ReadWithDefault(
            "Postleitzahl",
            gym.District == district
                ? gym.PostalCode
                : (1000 + (district * 10)).ToString(CultureInfo.InvariantCulture));

        ExecuteAndSave(() => _service.UpdateGym(gym, name, district, address, postalCode));
    }

    private void OpenWebsite(CatalogChain chain)
    {
        if (chain.Website is null)
        {
            output.WriteLine("Für diese Kette ist keine Website hinterlegt.");
            return;
        }

        OpenUrl(chain.Website, "Website");
    }

    private void OpenUrl(string url, string label)
    {
        try
        {
            browser.Open(url);
            output.WriteLine($"{label} wurde im Browser geöffnet.");
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            output.WriteLine($"{label} konnte nicht geöffnet werden: {exception.Message}");
            output.WriteLine(url);
        }
    }

    private DateOnly ReadVerificationDate()
    {
        var defaultDate = today();
        while (true)
        {
            output.Write($"Prüfdatum [{defaultDate:yyyy-MM-dd}]: ");
            var value = ReadLine()?.Trim();
            if (string.IsNullOrEmpty(value))
            {
                return defaultDate;
            }

            if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                return date;
            }

            output.WriteLine("Bitte das Datum als YYYY-MM-DD eingeben.");
        }
    }

    private int ReadDistrict(int? current)
    {
        while (true)
        {
            output.Write(current is null ? "Bezirk (1-23): " : $"Bezirk [{current}] (1-23): ");
            var value = ReadLine()?.Trim();
            if (string.IsNullOrEmpty(value) && current is not null)
            {
                return current.Value;
            }

            if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var district)
                && district is >= 1 and <= 23)
            {
                return district;
            }

            output.WriteLine("Bitte einen Bezirk zwischen 1 und 23 eingeben.");
        }
    }

    private string ReadRequired(string label)
    {
        while (true)
        {
            output.Write($"{label}: ");
            var value = ReadLine()?.Trim();
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }

            output.WriteLine($"{label} darf nicht leer sein.");
        }
    }

    private string? ReadOptional(string label)
    {
        output.Write($"{label}: ");
        var value = ReadLine()?.Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private string ReadWithDefault(string label, string current)
    {
        output.Write($"{label} [{current}]: ");
        var value = ReadLine()?.Trim();
        return string.IsNullOrEmpty(value) ? current : value;
    }

    private string ReadCommand()
    {
        output.Write("> ");
        return ReadLine()?.Trim() ?? "q";
    }

    private string? ReadLine()
    {
        var value = input.ReadLine();
        clearScreen();
        return value;
    }

    private void ExecuteAndSave(Action operation)
    {
        try
        {
            operation();
            Save();
        }
        catch (InvalidOperationException exception)
        {
            output.WriteLine($"Nicht gespeichert: {exception.Message}");
        }
    }

    private void Save()
    {
        catalogFile.Save(catalogPath, _service.Catalog);
        output.WriteLine("Gespeichert.");
    }

    private string FormatDate(DateOnly? date, DateOnly currentDate)
    {
        if (date is null)
        {
            return terminal.Red("-");
        }

        var formattedDate = date.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return VerificationStatus.IsStale(date.Value, currentDate)
            ? terminal.Orange(formattedDate)
            : formattedDate;
    }

    private string FormatClosureStatus(CatalogGym gym) =>
        gym.TemporarilyClosed == true
            ? $" | {terminal.Orange("vorübergehend geschlossen")}"
            : string.Empty;

    private static string FormatChainTags(CatalogChain chain)
    {
        if (chain.Tags is null || chain.Tags.Count == 0)
        {
            return string.Empty;
        }

        var labels = chain.Tags.Select(tag =>
            string.Equals(tag, "WomenOnly", StringComparison.Ordinal) ? "Nur für Frauen" : tag);
        return $" [{string.Join(", ", labels)}]";
    }

    private static bool TrySelect<T>(string command, IReadOnlyList<T> items, out T item)
    {
        if (int.TryParse(command, NumberStyles.None, CultureInfo.InvariantCulture, out var selection)
            && selection >= 1
            && selection <= items.Count)
        {
            item = items[selection - 1];
            return true;
        }

        item = default!;
        return false;
    }
}
