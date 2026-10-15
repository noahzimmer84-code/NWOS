namespace NWOS.Contracts;

/// <summary>
/// Erlaubt einem Modul, selbst eine Nachricht an ein anderes Modul
/// anzustoßen (Architekturdokument Abschnitt 6, Beispiel: "Der Benutzer gibt
/// Alya eine Sprachaufgabe. [...] Cardinal entscheidet, dass Marc die
/// eigentliche PC-Aufgabe ausführen soll."). Bewusst als schmale Schnittstelle
/// statt einer direkten Referenz auf <c>CardinalService</c>: ein Modul, das
/// nur <see cref="IModuleMessenger"/> kennt, kann technisch nicht mehr tun als
/// eine Nachricht einreichen - sie durchläuft trotzdem immer die reguläre
/// Berechtigungsprüfung (Abschnitt 9/31: kein Modul darf ein anderes direkt
/// durchgreifen, auch nicht auf Umwegen).
/// </summary>
public interface IModuleMessenger
{
    Task<ModuleMessageResult> SendAsync(ModuleMessage message, CancellationToken cancellationToken);
}

/// <summary>
/// Ergebnis eines von einem Modul initiierten Sendevorgangs. Bewusst
/// unabhängig von <c>NWOS.Cardinal.RoutingResult</c> definiert, damit
/// NWOS.Contracts (das auch von Modul-Projekten referenziert wird) keine
/// Abhängigkeit auf NWOS.Cardinal braucht - das würde einen Zirkelbezug
/// erzeugen (Cardinal referenziert Contracts, nicht umgekehrt).
/// </summary>
public enum ModuleMessageOutcome
{
    Delivered,
    Denied,
    PendingUserConfirmation,
    ModuleFailure,
}

public sealed record ModuleMessageResult(ModuleMessageOutcome Outcome, ModuleMessage? Response, string? Reason);
