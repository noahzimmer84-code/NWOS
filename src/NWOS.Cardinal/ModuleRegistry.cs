using System.Collections.Concurrent;
using NWOS.Contracts;

namespace NWOS.Cardinal;

/// <summary>
/// Hält die Menge der aktuell aktivierten Module (Architekturdokument Abschnitt 4:
/// "Nicht jeder Client muss dieselben Module aktiv haben"). Die Registry ist
/// bewusst dumm - sie trifft keine Entscheidungen, sondern verwaltet nur den
/// Zustand. Entscheidungen (starten/stoppen/routen) trifft <see cref="TaskRouter"/>.
/// </summary>
public sealed class ModuleRegistry
{
    private readonly ConcurrentDictionary<string, IModule> _modules = new();
    private readonly ConcurrentDictionary<string, PermissionScope> _grantedScopes = new();

    /// <summary>
    /// Registriert ein Modul mit den ihm laut Konfiguration erlaubten Scopes.
    /// Wird von NWOS.Host beim Start anhand der Konfiguration aus
    /// NWOS.ConfigSetup aufgerufen - nicht von den Modulen selbst, damit kein
    /// Modul sich eigenmächtig mehr Rechte geben kann.
    /// </summary>
    public void Register(IModule module, PermissionScope grantedScopes)
    {
        if (!_modules.TryAdd(module.ModuleId, module))
        {
            throw new InvalidOperationException(
                $"Modul '{module.ModuleId}' ist bereits registriert. Doppelte Registrierung deutet auf einen Konfigurationsfehler hin.");
        }

        _grantedScopes[module.ModuleId] = grantedScopes;
    }

    /// <summary>
    /// Vergibt Berechtigungen an eine Identität, die KEIN registriertes Modul
    /// ist (z. B. die lokale Desktop-UI, die über IPC zugreift). Bewusst eine
    /// eigene Methode statt <see cref="Register"/>: es gibt kein Objekt, das
    /// Cardinal starten/überwachen könnte, und der Aufruf ist im Code leicht
    /// auffindbar (Abschnitt 9: Berechtigungen müssen klar definiert sein).
    /// </summary>
    public void GrantExternalScope(string identityId, PermissionScope scope) =>
        _grantedScopes[identityId] = scope;

    public bool TryGet(string moduleId, out IModule module) =>
        _modules.TryGetValue(moduleId, out module!);

    public PermissionScope GetGrantedScopes(string moduleId) =>
        _grantedScopes.GetValueOrDefault(moduleId, PermissionScope.None);

    public IReadOnlyCollection<IModule> AllModules => (IReadOnlyCollection<IModule>)_modules.Values;

    public void Unregister(string moduleId)
    {
        _modules.TryRemove(moduleId, out _);
        _grantedScopes.TryRemove(moduleId, out _);
    }
}
