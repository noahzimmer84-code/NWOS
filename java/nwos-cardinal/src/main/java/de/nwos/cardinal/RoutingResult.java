package de.nwos.cardinal;

import de.nwos.contracts.ModuleMessage;

/**
 * Ergebnis des Routings einer Nachricht (Port von
 * NWOS.Cardinal.RoutingResult).
 */
public record RoutingResult(RoutingOutcome outcome, ModuleMessage response, String reason) {

    public static RoutingResult delivered(ModuleMessage response) {
        return new RoutingResult(RoutingOutcome.DELIVERED, response, null);
    }

    public static RoutingResult denied(String reason) {
        return new RoutingResult(RoutingOutcome.DENIED, null, reason);
    }

    public static RoutingResult pendingUserConfirmation() {
        return new RoutingResult(RoutingOutcome.PENDING_USER_CONFIRMATION, null, "Wartet auf Nutzerbestätigung.");
    }

    public static RoutingResult moduleFailure(String reason) {
        return new RoutingResult(RoutingOutcome.MODULE_FAILURE, null, reason);
    }
}
