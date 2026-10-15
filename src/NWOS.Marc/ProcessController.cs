using System.Diagnostics;

namespace NWOS.Marc;

/// <summary>
/// Startet/beendet Prozesse (Architekturdokument Abschnitt 9: "Programme
/// öffnen", "Programme schließen"). Nachrichten mit
/// <see cref="NWOS.Contracts.PermissionScope.ProcessControl"/> lösen laut
/// <c>PermissionManager</c> bereits eine Nutzerbestätigung aus, bevor
/// <see cref="MarcModule"/> überhaupt aufgerufen wird - diese Klasse selbst
/// muss sich um Berechtigungen nicht mehr kümmern und kann sich auf die
/// eigentliche Aktion konzentrieren.
/// </summary>
public sealed class ProcessController
{
    public int LaunchApplication(string executablePath, string arguments = "")
    {
        if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException("Ausführbare Datei nicht gefunden.", executablePath);
        }

        var startInfo = new ProcessStartInfo(executablePath, arguments)
        {
            UseShellExecute = true,
        };

        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Prozess '{executablePath}' konnte nicht gestartet werden.");

        return process.Id;
    }

    /// <summary>
    /// Beendet einen Prozess kontrolliert (erst <see cref="Process.CloseMainWindow"/>,
    /// nur bei Bedarf <see cref="Process.Kill()"/>) - ein hartes Kill ohne
    /// Versuch eines geordneten Schließens würde ungespeicherte Nutzerdaten
    /// riskieren, was Abschnitt 41 ("Sicherheit vor Komfort") widerspricht.
    /// </summary>
    public bool CloseApplication(int processId, TimeSpan? gracefulTimeout = null)
    {
        Process process;
        try
        {
            process = Process.GetProcessById(processId);
        }
        catch (ArgumentException)
        {
            return false; // Prozess existiert nicht (mehr).
        }

        if (process.CloseMainWindow())
        {
            if (process.WaitForExit((int)(gracefulTimeout ?? TimeSpan.FromSeconds(5)).TotalMilliseconds))
            {
                return true;
            }
        }

        process.Kill();
        return true;
    }
}
