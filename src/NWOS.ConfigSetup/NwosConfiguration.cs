using NWOS.Contracts;

namespace NWOS.ConfigSetup;

/// <summary>
/// Datenmodell für das Konfigurationsformular aus Abschnitt 4 des
/// Master-Entwicklungsauftrags. Für den kleinsten stabilen Kern werden nur die
/// Felder abgebildet, die Cardinal tatsächlich braucht, um zu wissen, welche
/// Module aktiviert werden sollen (Abschnitt 4: "Nicht jeder Client muss
/// dieselben Module aktiv haben"). Felder für spätere Phasen (Design,
/// Hintergrund, Geräteverbindungen im Detail, ...) sind als TODO markiert,
/// damit das Formular nicht mit ungenutzten Optionen vorgetäuscht wird
/// (Abschnitt 40: keine Fake-Funktionen).
/// </summary>
public sealed class NwosConfiguration
{
    public bool EnableDesktopShell { get; set; } = true;
    public bool EnableSmartBoxes { get; set; }
    public bool EnableWidgets { get; set; }
    public bool EnableAlya { get; set; }
    public bool EnableMarc { get; set; }
    public bool EnableAlice { get; set; } = true; // Sicherheit standardmäßig an (Abschnitt 43: Priorität 1)
    public bool EnableMultiDevice { get; set; }
    public bool EnableMaintenance { get; set; }
    public bool EnableSystemCenter { get; set; } = true;

    /// <summary>
    /// Zeitfenster, in dem Wartung stattfinden darf (Abschnitt 26: "Der
    /// Benutzer soll außerdem festlegen können, wann Wartung stattfinden darf").
    /// Null = kein Fenster konfiguriert, Wartung bleibt inaktiv statt zu raten.
    /// </summary>
    public TimeOnly? MaintenanceWindowStart { get; set; }
    public TimeOnly? MaintenanceWindowEnd { get; set; }

    // TODO(Phase 2/5): Design, Hintergrund, Personalisierung - gehören fachlich
    // zum Desktop-/Background-Engine-Modul und werden dort ergänzt, sobald
    // diese Module existieren, statt hier Platzhalter vorzuhalten.

    public PermissionScope ScopesFor(string moduleId) => moduleId switch
    {
        "nwos.alice" => PermissionScope.FileSystemRead | PermissionScope.FileSystemWrite | PermissionScope.SystemChange,
        "nwos.marc" => PermissionScope.FileSystemRead | PermissionScope.FileSystemWrite | PermissionScope.NetworkResearch | PermissionScope.ProcessControl,
        "nwos.alya" => PermissionScope.None, // Alya hat bewusst keine Systemrechte (Architektur Abschnitt 5)
        "nwos.desktopshell" => PermissionScope.FileSystemRead | PermissionScope.FileSystemWrite,
        "nwos.widgets" => PermissionScope.FileSystemRead | PermissionScope.FileSystemWrite,
        "nwos.systemcenter" => PermissionScope.FileSystemRead | PermissionScope.FileSystemWrite | PermissionScope.ProcessControl,
        _ => PermissionScope.None,
    };
}
