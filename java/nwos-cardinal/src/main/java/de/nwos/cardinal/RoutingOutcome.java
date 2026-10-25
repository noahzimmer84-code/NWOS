package de.nwos.cardinal;

import de.nwos.contracts.ModuleMessage;

/** Ausgang des Routings einer Nachricht (Port von NWOS.Cardinal.RoutingOutcome). */
public enum RoutingOutcome {
    DELIVERED,
    DENIED,
    PENDING_USER_CONFIRMATION,
    MODULE_FAILURE
}
