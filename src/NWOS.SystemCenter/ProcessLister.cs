using System.Diagnostics;

namespace NWOS.SystemCenter;

public sealed record ProcessInfo(int ProcessId, string Name, long WorkingSetBytes, bool IsProtected);

/// <summary>
/// Listet laufende Prozesse (Architekturdokument Abschnitt 24: "aktive
/// Prozesse"). Nutzt bewusst nur <see cref="System.Diagnostics.Process"/>
/// (plattformneutral, im Gegensatz zu WMI) - reine Auflistung braucht keine
/// Windows-spezifische API.
/// </summary>
public sealed class ProcessLister
{
    public IReadOnlyList<ProcessInfo> ListProcesses()
    {
        var result = new List<ProcessInfo>();

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                result.Add(new ProcessInfo(
                    process.Id,
                    process.ProcessName,
                    SafeWorkingSet(process),
                    ProtectedProcessList.IsProtected(process.ProcessName)));
            }
            finally
            {
                process.Dispose();
            }
        }

        return result;
    }

    private static long SafeWorkingSet(Process process)
    {
        try
        {
            return process.WorkingSet64;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // Zugriff auf manche Systemprozesse ist ohne erhöhte Rechte nicht
            // möglich (Abschnitt 41: keine automatische Rechteeskalation) -
            // 0 statt Abbruch der gesamten Auflistung.
            return 0;
        }
    }
}
