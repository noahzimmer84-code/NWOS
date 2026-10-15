using System.Runtime.Versioning;
using Microsoft.Win32;

namespace NWOS.SystemCenter.Windows;

/// <summary>
/// Verwaltet Autostart-Einträge über den dokumentierten, pro Benutzer
/// geltenden Windows-Mechanismus
/// (HKCU\Software\Microsoft\Windows\CurrentVersion\Run) - bewusst NICHT den
/// systemweiten HKLM-Pfad, aus demselben Grund wie bei
/// NWOS.WindowsIntegration.ShellIntegration: geringstmöglicher Eingriff,
/// nur für den aktuellen Benutzer.
///
/// Deaktivieren verschiebt den Wert in einen eigenen NWOS-Unterschlüssel
/// statt ihn zu löschen (Abschnitt 41: Reversibilität) - erneutes Aktivieren
/// stellt ihn unverändert wieder her.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class RegistryAutostartManager : IAutostartManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string DisabledKeyPath = @"Software\NWOS\DisabledAutostart";

    public Task<IReadOnlyList<AutostartEntry>> ListAsync(CancellationToken cancellationToken)
    {
        var entries = new List<AutostartEntry>();

        using (var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false))
        {
            if (runKey is not null)
            {
                foreach (var name in runKey.GetValueNames())
                {
                    var commandLine = runKey.GetValue(name) as string ?? string.Empty;
                    entries.Add(new AutostartEntry(name, commandLine, IsEnabled: true));
                }
            }
        }

        using (var disabledKey = Registry.CurrentUser.OpenSubKey(DisabledKeyPath, writable: false))
        {
            if (disabledKey is not null)
            {
                foreach (var name in disabledKey.GetValueNames())
                {
                    var commandLine = disabledKey.GetValue(name) as string ?? string.Empty;
                    entries.Add(new AutostartEntry(name, commandLine, IsEnabled: false));
                }
            }
        }

        return Task.FromResult<IReadOnlyList<AutostartEntry>>(entries);
    }

    public Task SetEnabledAsync(string name, bool isEnabled, CancellationToken cancellationToken)
    {
        return isEnabled ? MoveAsync(DisabledKeyPath, RunKeyPath, name) : MoveAsync(RunKeyPath, DisabledKeyPath, name);
    }

    private static Task MoveAsync(string fromKeyPath, string toKeyPath, string name)
    {
        using var fromKey = Registry.CurrentUser.OpenSubKey(fromKeyPath, writable: true);
        var value = fromKey?.GetValue(name) as string;

        if (value is null)
        {
            throw new InvalidOperationException($"Autostart-Eintrag '{name}' wurde nicht unter '{fromKeyPath}' gefunden.");
        }

        using (var toKey = Registry.CurrentUser.CreateSubKey(toKeyPath, writable: true))
        {
            toKey!.SetValue(name, value, RegistryValueKind.String);
        }

        fromKey!.DeleteValue(name);
        return Task.CompletedTask;
    }
}
