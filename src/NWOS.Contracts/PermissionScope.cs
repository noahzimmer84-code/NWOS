namespace NWOS.Contracts;

/// <summary>
/// Grobe Berechtigungskategorien gemäß Architekturdokument Abschnitt 9
/// (Berechtigungssystem). Ein Modul deklariert beim Registrieren bei Cardinal,
/// welche Scopes es grundsätzlich benötigen darf; jede konkrete Aktion wird
/// zusätzlich vom <see cref="NWOS.Cardinal.PermissionManager"/> einzeln geprüft.
/// </summary>
[Flags]
public enum PermissionScope
{
    None = 0,
    FileSystemRead = 1 << 0,
    FileSystemWrite = 1 << 1,
    ProcessControl = 1 << 2,
    NetworkResearch = 1 << 3, // eng gefasste, whitelistende Recherche (Marc, Punkt 9)
    NetworkGeneral = 1 << 4, // bewusst NICHT an Marc vergeben
    SystemChange = 1 << 5, // erfordert i.d.R. zusätzliche Nutzerfreigabe pro Aktion
    DeviceCommunication = 1 << 6, // Multi-Device, Abschnitt 8
}

/// <summary>
/// Ergebnis einer Berechtigungsprüfung inkl. Begründung, damit Entscheidungen
/// nachvollziehbar protokolliert werden können (Abschnitt 8: "Sicherheitsrelevante
/// Aktionen müssen nachvollziehbar protokolliert werden").
/// </summary>
public sealed record PermissionDecision(bool Allowed, string Reason, bool RequiresUserConfirmation);
