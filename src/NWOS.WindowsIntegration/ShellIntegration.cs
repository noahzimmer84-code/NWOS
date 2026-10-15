using System.Runtime.Versioning;
using Microsoft.Win32;

namespace NWOS.WindowsIntegration;

/// <summary>
/// Minimalimplementierung für den kleinsten stabilen Kern (Architekturdokument
/// Abschnitt 13 / Abschnitt 4). Nutzt bewusst den offiziellen, dokumentierten
/// **Pro-Benutzer**-Shell-Override von Windows
/// (HKCU\Software\Microsoft\Windows NT\CurrentVersion\Winlogon, Wert "Shell"),
/// NICHT die systemweite HKLM-Einstellung. Das ist der reversibelste,
/// am wenigsten invasive Weg, eine alternative Shell zu registrieren, und
/// betrifft nur den aktuellen Windows-Benutzer, nicht die ganze Maschine.
///
/// Diese Klasse ersetzt nicht explorer.exe-Funktionalität selbst (das ist
/// Aufgabe des Desktop-Shell-Moduls aus Phase 2) - sie sorgt nur dafür, dass
/// beim Login der NWOS-Host statt explorer.exe gestartet wird.
///
/// TODO(Phase 1, vor Produktivsetzung): Diese Implementierung ist ungetestet,
/// da diese Entwicklungsumgebung kein Windows ist. Vor echtem Einsatz:
/// - Verhalten bei fehlenden Registry-Rechten (kein Admin nötig für HKCU, aber
///   Gruppenrichtlinien können den Wert sperren) abfangen und dem Nutzer klar
///   melden statt stillschweigend zu scheitern.
/// - Tatsächlichen Installationspfad des NWOS-Host-Executables robust ermitteln
///   (aktuell Platzhalter-Parameter im Konstruktor).
/// - End-to-End-Test: Abmelden/Anmelden mit gesetztem Override, inkl. Rückfall
///   über RestoreDefaultShellAsync aus einer zweiten, unabhängig startfähigen
///   Recovery-Routine (Abschnitt 4: Reversibilität ist Pflicht, kein Nice-to-have).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class ShellIntegration : IShellIntegration
{
    private const string WinlogonKeyPath = @"Software\Microsoft\Windows NT\CurrentVersion\Winlogon";
    private const string ShellValueName = "Shell";
    private const string DefaultShellExecutable = "explorer.exe";

    private readonly string _nwosHostExecutablePath;

    public ShellIntegration(string nwosHostExecutablePath)
    {
        if (string.IsNullOrWhiteSpace(nwosHostExecutablePath))
        {
            throw new ArgumentException("Pfad zur NWOS-Host-Executable darf nicht leer sein.", nameof(nwosHostExecutablePath));
        }

        _nwosHostExecutablePath = nwosHostExecutablePath;
    }

    public Task<bool> IsNwosShellActiveAsync(CancellationToken cancellationToken)
    {
        using var key = Registry.CurrentUser.OpenSubKey(WinlogonKeyPath, writable: false);
        var currentValue = key?.GetValue(ShellValueName) as string;
        return Task.FromResult(
            currentValue is not null &&
            currentValue.Equals(_nwosHostExecutablePath, StringComparison.OrdinalIgnoreCase));
    }

    public Task RegisterNwosShellAsync(CancellationToken cancellationToken)
    {
        using var key = Registry.CurrentUser.CreateSubKey(WinlogonKeyPath, writable: true)
            ?? throw new InvalidOperationException(
                "Winlogon-Registrierungsschlüssel konnte nicht geöffnet/erstellt werden. " +
                "Mögliche Ursache: Gruppenrichtlinie sperrt diesen Wert. Keine automatische " +
                "Eskalation von Rechten - das widerspräche Abschnitt 41 (Sicherheit vor Komfort).");

        key.SetValue(ShellValueName, _nwosHostExecutablePath, RegistryValueKind.String);
        return Task.CompletedTask;
    }

    public Task RestoreDefaultShellAsync(CancellationToken cancellationToken)
    {
        using var key = Registry.CurrentUser.CreateSubKey(WinlogonKeyPath, writable: true);
        key?.SetValue(ShellValueName, DefaultShellExecutable, RegistryValueKind.String);
        return Task.CompletedTask;
    }
}
