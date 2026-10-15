using NWOS.Host;
using NWOS.SystemCenter.Windows;
using NWOS.WindowsIntegration;

namespace NWOS.Bootstrap.Windows;

/// <summary>
/// Einstiegspunkt-Logik für dieses Executable. Getrennt von
/// <c>Program.Main</c>, damit die Kommandobehandlung ohne einen echten
/// Prozessstart testbar bliebe, falls dafür später Tests sinnvoll werden
/// (aktuell keine, da alle Aufrufe direkt Windows-APIs anfassen - siehe
/// README für die manuellen Prüfschritte).
///
/// Subcommands (Argument 0):
///   (keine)          -> normaler NWOS-Start (Cardinal + alle konfigurierten Module,
///                       inkl. echter WMI-/Registry-Datenquellen für System Center)
///   shell-status      -> zeigt, ob NWOS aktuell als Windows-Shell registriert ist
///   register-shell     -> registriert NWOS als Shell FÜR DEN AKTUELLEN BENUTZER,
///                       nur nach expliziter Tipp-Bestätigung (Abschnitt 41)
///   restore-shell      -> stellt explorer.exe als Shell wieder her (keine
///                       Bestätigung nötig - das ist der Sicherheits-Ausweg)
/// </summary>
public static class BootstrapCommands
{
    public static async Task<int> RunAsync(string[] args)
    {
        var command = args.Length > 0 ? args[0] : "run";

        return command switch
        {
            "run" => await RunNormalAsync(args),
            "shell-status" => await ShowShellStatusAsync(),
            "register-shell" => await RegisterShellAsync(),
            "restore-shell" => await RestoreShellAsync(),
            "--help" or "-h" or "help" => PrintUsage(),
            _ => PrintUnknownCommand(command),
        };
    }

    private static async Task<int> RunNormalAsync(string[] args)
    {
        // Nur eine Cardinal-Instanz pro Windows-Benutzer: Autostart und
        // Start-Verknüpfung könnten sonst beide einen Kern starten (doppelte
        // Pipe, doppelte Alice-Überwachung).
        using var singleInstance = new Mutex(false, @"Local\NWOS.Cardinal.SingleInstance", out var isFirstInstance);
        if (!isFirstInstance)
        {
            Console.WriteLine("NWOS (Cardinal) läuft bereits - zweite Instanz wird nicht gestartet.");
            return 0;
        }

        var windowsExtensions = new WindowsExtensions(
            StatusProvider: new WmiSystemStatusProvider(),
            AutostartManager: new RegistryAutostartManager());

        return await HostComposition.RunAsync(args, windowsExtensions);
    }

    private static ShellIntegration CreateShellIntegration()
    {
        var currentExecutablePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Konnte eigenen Programmpfad nicht bestimmen (Environment.ProcessPath ist null).");

        return new ShellIntegration(currentExecutablePath);
    }

    private static async Task<int> ShowShellStatusAsync()
    {
        var isActive = await CreateShellIntegration().IsNwosShellActiveAsync(CancellationToken.None);
        Console.WriteLine(isActive
            ? "NWOS ist aktuell als Windows-Shell für diesen Benutzer registriert."
            : "NWOS ist aktuell NICHT als Windows-Shell registriert (Standard: explorer.exe).");
        return 0;
    }

    private static async Task<int> RegisterShellAsync()
    {
        // Abschnitt 41 ("Sicherheit vor Komfort") und Abschnitt 4 verlangen
        // ausdrücklich, dass so eine tiefgreifende Änderung nicht beiläufig
        // passiert. Eine einzelne Ja/Nein-Taste reicht dafür nicht - es muss
        // ein exaktes Wort getippt werden, damit ein versehentlicher Enter-
        // Druck (z. B. aus einem Skript) nichts auslöst.
        Console.WriteLine("ACHTUNG: Dies ändert die Anmelde-Shell für DEINEN Windows-Benutzerkonto.");
        Console.WriteLine("Nach dem nächsten Login startet NWOS statt des normalen Windows-Desktops.");
        Console.WriteLine("Das lässt sich jederzeit rückgängig machen mit: nwos-bootstrap restore-shell");
        Console.Write("Zum Bestätigen exakt 'BESTAETIGEN' eingeben: ");
        var input = Console.ReadLine();

        if (!string.Equals(input?.Trim(), "BESTAETIGEN", StringComparison.Ordinal))
        {
            Console.WriteLine("Abgebrochen - keine Änderung vorgenommen.");
            return 1;
        }

        await CreateShellIntegration().RegisterNwosShellAsync(CancellationToken.None);
        Console.WriteLine("NWOS wurde als Shell registriert. Wirksam ab dem nächsten Login.");
        Console.WriteLine("Falls etwas nicht funktioniert: nwos-bootstrap restore-shell (auch aus dem abgesicherten Modus möglich).");
        return 0;
    }

    private static async Task<int> RestoreShellAsync()
    {
        // Bewusst OHNE Bestätigungsabfrage: das ist der Notausgang, der
        // gerade dann funktionieren muss, wenn etwas schiefgelaufen ist.
        // Eine zusätzliche Hürde hier wäre kontraproduktiv zu Abschnitt 41.
        await CreateShellIntegration().RestoreDefaultShellAsync(CancellationToken.None);
        Console.WriteLine("explorer.exe wurde als Standard-Shell wiederhergestellt. Wirksam ab dem nächsten Login.");
        return 0;
    }

    private static int PrintUsage()
    {
        Console.WriteLine("""
            NWOS Bootstrap (Windows)

            Verwendung:
              nwos-bootstrap [run]           NWOS normal starten (Standard ohne Argument)
              nwos-bootstrap shell-status    Zeigt, ob NWOS als Windows-Shell aktiv ist
              nwos-bootstrap register-shell  Registriert NWOS als Windows-Shell (mit Bestätigung)
              nwos-bootstrap restore-shell   Stellt explorer.exe als Shell wieder her
            """);
        return 0;
    }

    private static int PrintUnknownCommand(string command)
    {
        Console.WriteLine($"Unbekannter Befehl: '{command}'");
        PrintUsage();
        return 1;
    }
}
