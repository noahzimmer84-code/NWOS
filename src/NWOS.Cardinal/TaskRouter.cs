using Microsoft.Extensions.Logging;
using NWOS.Contracts;

namespace NWOS.Cardinal;

/// <summary>
/// Vermittelt jede Nachricht zwischen Modulen (Architekturdokument Abschnitt 7:
/// "Kein Modul darf ein anderes Modul direkt durchgreifen - jede Nachricht läuft
/// über Cardinal"). Der Router selbst enthält keine Fachlogik der Module,
/// sondern nur: Berechtigung prüfen -> zustellen -> Ergebnis zurückgeben.
/// </summary>
public sealed class TaskRouter
{
    private readonly ModuleRegistry _registry;
    private readonly PermissionManager _permissions;
    private readonly ILogger<TaskRouter> _logger;

    public TaskRouter(ModuleRegistry registry, PermissionManager permissions, ILogger<TaskRouter> logger)
    {
        _registry = registry;
        _permissions = permissions;
        _logger = logger;
    }

    /// <summary>
    /// Sendet eine Nachricht an das Zielmodul, sofern die Berechtigungsprüfung
    /// dies erlaubt. Gibt <c>null</c> zurück, wenn die Nachricht abgelehnt wurde
    /// oder das Zielmodul nicht registriert/aktiv ist - der Aufrufer bekommt in
    /// dem Fall keine stille Fehlfunktion, sondern muss den Fall explizit
    /// behandeln.
    /// </summary>
    public async Task<RoutingResult> RouteAsync(ModuleMessage message, CancellationToken cancellationToken)
    {
        var decision = _permissions.Evaluate(message);
        if (!decision.Allowed)
        {
            return RoutingResult.Denied(decision.Reason);
        }

        if (decision.RequiresUserConfirmation)
        {
            // TODO(Phase 2 UI): An dieser Stelle muss eine echte Nutzerabfrage
            // eingehängt werden (z. B. über Alya/Desktop-Shell-Benachrichtigung).
            // Für den kleinsten stabilen Kern wird die Aktion bewusst NICHT
            // automatisch durchgewunken, sondern als "wartend" zurückgegeben -
            // das ist die sichere Grundeinstellung (Abschnitt 41: "Sicherheit
            // vor Komfort").
            _logger.LogInformation(
                "Nachricht {MessageId} von {Source} an {Target} wartet auf Nutzerbestätigung.",
                message.MessageId, message.SourceModuleId, message.TargetModuleId);
            return RoutingResult.PendingUserConfirmation();
        }

        if (!_registry.TryGet(message.TargetModuleId, out var targetModule))
        {
            _logger.LogWarning(
                "Zielmodul {Target} für Nachricht {MessageId} ist nicht registriert/aktiv.",
                message.TargetModuleId, message.MessageId);
            return RoutingResult.Denied($"Zielmodul '{message.TargetModuleId}' ist nicht aktiv.");
        }

        try
        {
            var response = await targetModule.HandleMessageAsync(message, cancellationToken);
            return RoutingResult.Delivered(response);
        }
        catch (Exception ex)
        {
            // Abschnitt 36: ein abstürzendes Modul darf nicht Cardinal mitreißen.
            _logger.LogError(ex,
                "Modul {Target} hat Nachricht {MessageId} nicht verarbeitet - wird isoliert behandelt.",
                message.TargetModuleId, message.MessageId);
            return RoutingResult.ModuleFailure(ex.Message);
        }
    }
}

public enum RoutingOutcome
{
    Delivered,
    Denied,
    PendingUserConfirmation,
    ModuleFailure,
}

public sealed record RoutingResult(RoutingOutcome Outcome, ModuleMessage? Response, string? Reason)
{
    public static RoutingResult Delivered(ModuleMessage? response) =>
        new(RoutingOutcome.Delivered, response, null);

    public static RoutingResult Denied(string reason) =>
        new(RoutingOutcome.Denied, null, reason);

    public static RoutingResult PendingUserConfirmation() =>
        new(RoutingOutcome.PendingUserConfirmation, null, "Wartet auf Nutzerbestätigung.");

    public static RoutingResult ModuleFailure(string reason) =>
        new(RoutingOutcome.ModuleFailure, null, reason);
}
