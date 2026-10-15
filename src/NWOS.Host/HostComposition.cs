using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NWOS.Alice;
using NWOS.Alya;
using NWOS.Cardinal;
using NWOS.ConfigSetup;
using NWOS.DesktopShell;
using NWOS.Marc;
using NWOS.Settings;
using NWOS.SystemCenter;
using NWOS.Widgets;

namespace NWOS.Host;

/// <summary>
/// Einstiegspunkt-Logik für NWOS (Architekturdokument Abschnitt 13/44). Ablauf:
///   1. Konfiguration laden, oder falls keine existiert: Konfigurationsformular
///      durchlaufen und speichern (Abschnitt 4).
///   2. Cardinal-Kern (Registry, Router, Permissions, Health-Monitor) starten,
///      zusammen mit dem IPC-Server (Abschnitt 7), damit andere lokale
///      NWOS-Prozesse (z. B. eine künftig umgebaute Desktop-Shell-UI) Cardinal
///      über <c>NWOS.Ipc.IpcClient</c> ansprechen können, ohne In-Process zu laufen.
///   3. Module gemäß Konfiguration registrieren - jedes nur, wenn aktiviert
///      (Abschnitt 4: "Nicht jeder Client muss dieselben Module aktiv haben").
///
/// Bewusst als eigene, öffentliche Klasse statt direkt in <c>Program.Main</c>:
/// so kann ein Windows-spezifisches Bootstrap-Projekt (NWOS.Bootstrap.Windows)
/// dieselbe Komposition wiederverwenden und nur die Windows-Erweiterungen
/// (<see cref="WindowsExtensions"/>) ergänzen, statt die gesamte
/// Registrierungslogik zu duplizieren (Abschnitt 39: bestehenden Code
/// bevorzugt erweitern statt ersetzen).
///
/// Absichtlich NICHT enthalten: Windows-Shell-Registrierung. Das ist ein
/// separater, expliziter Schritt im Bootstrap-Projekt, passiert nie
/// automatisch beim Start - Abschnitt 41: "Sicherheit vor Komfort".
/// </summary>
public static class HostComposition
{
    public static async Task<int> RunAsync(string[] args, WindowsExtensions? windowsExtensions = null)
    {
        var configDirectory = Environment.GetEnvironmentVariable("NWOS_CONFIG_DIR")
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NWOS");

        var configStore = new ConfigStore(configDirectory);

        NwosConfiguration configuration;
        if (configStore.ConfigurationExists())
        {
            configuration = await configStore.LoadAsync(CancellationToken.None);
        }
        else
        {
            Console.WriteLine("Keine NWOS-Konfiguration gefunden - Ersteinrichtung wird gestartet.");
            var wizard = new FirstRunWizard(new ConsoleUserPrompt());
            configuration = await wizard.RunAsync(CancellationToken.None);
            await configStore.SaveAsync(configuration, CancellationToken.None);
            Console.WriteLine($"Konfiguration gespeichert unter: {configDirectory}");
        }

        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(args);

        builder.Services.AddSingleton(configuration);
        builder.Services.AddSingleton<ModuleRegistry>();
        builder.Services.AddSingleton<PermissionManager>();
        builder.Services.AddSingleton<TaskRouter>();
        builder.Services.AddSingleton<HealthMonitor>();
        builder.Services.AddSingleton<CardinalService>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<CardinalService>());
        builder.Services.AddHostedService<IpcHostedService>();

        var host = builder.Build();

        var logger = host.Services.GetRequiredService<ILogger<CardinalService>>();
        logger.LogInformation("NWOS-Kern startet.{WindowsExtensions}",
            windowsExtensions is null ? "" : " (mit Windows-Erweiterungen)");

        // Settings-Store wird unabhängig von der Modulauswahl immer geladen -
        // das zentrale Einstellungszentrum (Abschnitt 33) existiert unabhängig
        // davon, welche Fach-Module aktiv sind.
        var settingsStore = new SettingsStore(configDirectory);
        await settingsStore.LoadAsync(CancellationToken.None);

        var cardinal = host.Services.GetRequiredService<CardinalService>();

        // Externe Prozesse (z. B. die Desktop-UI) erscheinen für Cardinal immer
        // als "external.ipc" (siehe IpcServer). Bewusst NUR Datei lesen/schreiben
        // - dieselben Rechte wie Desktop-Shell und Widgets. Kein ProcessControl,
        // kein SystemChange, kein Netzwerk: selbst ein manipulierter Client
        // kann über die Pipe weder Prozesse steuern noch Quarantäne/Autostart
        // ändern (Abschnitt 9/41).
        host.Services.GetRequiredService<ModuleRegistry>().GrantExternalScope(
            NWOS.Ipc.IpcDefaults.ExternalCallerIdentity,
            NWOS.Contracts.PermissionScope.FileSystemRead | NWOS.Contracts.PermissionScope.FileSystemWrite);

        if (configuration.EnableDesktopShell)
        {
            var desktopShell = new DesktopShellModule(configDirectory, settingsStore);
            cardinal.RegisterModule(desktopShell, configuration.ScopesFor(desktopShell.ModuleId));
            logger.LogInformation("Modul {Module} registriert.", desktopShell.ModuleId);
        }
        else
        {
            logger.LogInformation("Desktop-Shell-Modul laut Konfiguration deaktiviert - wird nicht registriert.");
        }

        if (configuration.EnableWidgets)
        {
            var widgets = new WidgetsModule(configDirectory);
            cardinal.RegisterModule(widgets, configuration.ScopesFor(widgets.ModuleId));
            logger.LogInformation("Modul {Module} registriert.", widgets.ModuleId);
        }
        else
        {
            logger.LogInformation("Widget-Modul laut Konfiguration deaktiviert - wird nicht registriert.");
        }

        if (configuration.EnableAlice)
        {
            var downloadsDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            var aliceStateDirectory = Path.Combine(configDirectory, "alice");

            var alice = new AliceModule(downloadsDirectory, aliceStateDirectory);
            cardinal.RegisterModule(alice, configuration.ScopesFor(alice.ModuleId));
            logger.LogInformation("Modul {Module} registriert (überwacht {Directory}).", alice.ModuleId, downloadsDirectory);
        }
        else
        {
            logger.LogInformation("Alice (Sicherheit) laut Konfiguration deaktiviert - wird nicht registriert. " +
                                   "Hinweis: Sicherheit ist Priorität 1 (Abschnitt 43) - Deaktivierung ist Nutzerentscheidung, keine Empfehlung.");
        }

        if (configuration.EnableMarc)
        {
            var marc = new MarcModule();
            cardinal.RegisterModule(marc, configuration.ScopesFor(marc.ModuleId));
            logger.LogInformation("Modul {Module} registriert.", marc.ModuleId);
        }
        else
        {
            logger.LogInformation("Marc laut Konfiguration deaktiviert - wird nicht registriert.");
        }

        if (configuration.EnableAlya)
        {
            // Cardinal selbst wird als IModuleMessenger übergeben - Alya kann
            // dadurch Nachrichten an andere Module anstoßen (z. B. Alice für
            // den Lagebericht), aber ausschließlich über denselben,
            // berechtigungsgeprüften Weg wie jede andere Nachricht (Abschnitt 31).
            var alya = new AlyaModule(settingsStore, messenger: cardinal);
            cardinal.RegisterModule(alya, configuration.ScopesFor(alya.ModuleId));
            logger.LogInformation("Modul {Module} registriert.", alya.ModuleId);
        }
        else
        {
            logger.LogInformation("Alya laut Konfiguration deaktiviert - wird nicht registriert.");
        }

        if (configuration.EnableSystemCenter)
        {
            // Windows-Erweiterungen (WMI/Registry) kommen ausschließlich über
            // den windowsExtensions-Parameter - dieser plattformneutrale Code
            // referenziert nie direkt NWOS.SystemCenter.Windows (siehe
            // NWOS.Host.csproj). Ohne Bootstrap-Projekt: getestetes,
            // transparentes "nicht verfügbar"-Fallback statt Fake-Daten.
            var systemCenter = new SystemCenterModule(
                windowsExtensions?.StatusProvider,
                windowsExtensions?.AutostartManager,
                messenger: cardinal);
            cardinal.RegisterModule(systemCenter, configuration.ScopesFor(systemCenter.ModuleId));
            logger.LogInformation(
                "Modul {Module} registriert{Extra}.",
                systemCenter.ModuleId,
                windowsExtensions?.StatusProvider is null ? " (ohne Windows-spezifische Datenquellen)" : " (mit echten Windows-Datenquellen)");
        }
        else
        {
            logger.LogInformation("System Center laut Konfiguration deaktiviert - wird nicht registriert.");
        }

        try
        {
            await host.RunAsync();
        }
        finally
        {
            // Settings werden unabhängig vom Cardinal-Lebenszyklus gesichert,
            // damit ein Absturz eines Fach-Moduls (Abschnitt 36) nicht dazu
            // führt, dass zwischenzeitlich geänderte Einstellungen verloren gehen.
            await settingsStore.SaveAsync(CancellationToken.None);
        }

        return 0;
    }
}
