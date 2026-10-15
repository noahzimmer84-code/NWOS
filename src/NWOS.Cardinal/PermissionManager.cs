using Microsoft.Extensions.Logging;
using NWOS.Contracts;

namespace NWOS.Cardinal;

/// <summary>
/// Zentrale, verbindliche Berechtigungsprüfung (Architekturdokument Abschnitt 9).
/// Jede Nachricht zwischen Modulen MUSS diesen Manager durchlaufen -
/// <see cref="TaskRouter"/> ruft ihn vor jeder Zustellung auf. Es gibt bewusst
/// keinen Umgehungspfad.
/// </summary>
public sealed class PermissionManager
{
    private readonly ModuleRegistry _registry;
    private readonly ILogger<PermissionManager> _logger;

    // Feste Regel aus Abschnitt 9: Netzwerkzugriff über die enge Recherche-
    // Whitelist ja, allgemeiner Netzwerkzugriff nein - unabhängig davon, was
    // eine spätere Konfiguration versehentlich zuweisen würde. Das ist bewusst
    // hart codiert, nicht konfigurierbar (Abschnitt 9, Marc-Regel).
    private static readonly IReadOnlySet<string> ModulesDeniedGeneralNetwork =
        new HashSet<string> { "nwos.marc" };

    public PermissionManager(ModuleRegistry registry, ILogger<PermissionManager> logger)
    {
        _registry = registry;
        _logger = logger;
    }

    public PermissionDecision Evaluate(ModuleMessage message)
    {
        // Der vom Absender deklarierte Scope ist nur eine Untergrenze: der
        // tatsächlich nötige Scope ergibt sich zusätzlich aus dem MessageType
        // (MessageScopePolicy). Sonst könnte ein Absender einfach
        // PermissionScope.None angeben und die Prüfung umgehen.
        message = message with
        {
            RequiredScope = message.RequiredScope | MessageScopePolicy.MinimumScopeFor(message.MessageType),
        };

        var granted = _registry.GetGrantedScopes(message.SourceModuleId);

        if (message.RequiredScope == PermissionScope.NetworkGeneral &&
            ModulesDeniedGeneralNetwork.Contains(message.SourceModuleId))
        {
            var decision = new PermissionDecision(
                Allowed: false,
                Reason: $"Modul '{message.SourceModuleId}' darf laut fester Sicherheitsregel keinen allgemeinen Netzwerkzugriff durchführen (siehe Architektur Abschnitt 9).",
                RequiresUserConfirmation: false);

            LogDecision(message, decision);
            return decision;
        }

        if (!granted.HasFlag(message.RequiredScope))
        {
            var decision = new PermissionDecision(
                Allowed: false,
                Reason: $"Modul '{message.SourceModuleId}' besitzt den Scope '{message.RequiredScope}' nicht.",
                RequiresUserConfirmation: false);

            LogDecision(message, decision);
            return decision;
        }

        // Systemändernde Aktionen sind grundsätzlich erlaubt (Scope vorhanden),
        // erfordern laut Abschnitt 8/25 aber zusätzlich eine Nutzerbestätigung,
        // sofern sie nicht auf der (an dieser Stelle bewusst noch nicht
        // implementierten) Liste eindeutig sicherer Aktionen stehen.
        // TODO(Phase 3/4): Liste "eindeutig sicher automatisierbarer" Aktionen
        // versioniert hinterlegen (Abschnitt 8) statt hier pauschal zu verlangen.
        var requiresConfirmation = message.RequiredScope.HasFlag(PermissionScope.SystemChange)
            || message.RequiredScope.HasFlag(PermissionScope.ProcessControl);

        var allowedDecision = new PermissionDecision(
            Allowed: true,
            Reason: "Scope vorhanden.",
            RequiresUserConfirmation: requiresConfirmation);

        LogDecision(message, allowedDecision);
        return allowedDecision;
    }

    private void LogDecision(ModuleMessage message, PermissionDecision decision)
    {
        // Abschnitt 8: sicherheitsrelevante Entscheidungen müssen nachvollziehbar
        // protokolliert werden. Hier zunächst strukturiertes Logging;
        // TODO(Phase 3): persistentes, für Alice/Nutzer einsehbares Audit-Log.
        _logger.LogInformation(
            "Permission {Result} for {Source} -> {Target} ({MessageType}, scope={Scope}): {Reason}",
            decision.Allowed ? "GRANTED" : "DENIED",
            message.SourceModuleId,
            message.TargetModuleId,
            message.MessageType,
            message.RequiredScope,
            decision.Reason);
    }
}
