using System.Text.Json;

namespace NWOS.ConfigSetup;

/// <summary>
/// Persistiert die Konfiguration ausschließlich lokal als JSON
/// (Architekturdokument Abschnitt 6: "Lokale Datenhaltung als Standard").
/// Kein Netzwerkzugriff, keine Telemetrie - diese Klasse fasst das
/// Dateisystem nur im übergebenen Konfigurationsverzeichnis an.
/// </summary>
public sealed class ConfigStore
{
    private readonly string _configFilePath;
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    public ConfigStore(string configDirectory)
    {
        if (string.IsNullOrWhiteSpace(configDirectory))
        {
            throw new ArgumentException("Konfigurationsverzeichnis darf nicht leer sein.", nameof(configDirectory));
        }

        Directory.CreateDirectory(configDirectory);
        _configFilePath = Path.Combine(configDirectory, "nwos.config.json");
    }

    public bool ConfigurationExists() => File.Exists(_configFilePath);

    public async Task<NwosConfiguration> LoadAsync(CancellationToken cancellationToken)
    {
        if (!ConfigurationExists())
        {
            throw new FileNotFoundException(
                "Keine Konfiguration vorhanden. Vor dem ersten Start muss das Konfigurationsformular " +
                "(Abschnitt 4) durchlaufen und gespeichert werden - es gibt bewusst keinen stillen " +
                "Standardwert-Fallback, das widerspräche Abschnitt 4 des Auftrags.",
                _configFilePath);
        }

        await using var stream = File.OpenRead(_configFilePath);
        var config = await JsonSerializer.DeserializeAsync<NwosConfiguration>(stream, cancellationToken: cancellationToken);
        return config ?? throw new InvalidDataException($"Konfigurationsdatei '{_configFilePath}' ist leer oder ungültig.");
    }

    public async Task SaveAsync(NwosConfiguration configuration, CancellationToken cancellationToken)
    {
        await using var stream = File.Create(_configFilePath);
        await JsonSerializer.SerializeAsync(stream, configuration, SerializerOptions, cancellationToken);
    }
}
