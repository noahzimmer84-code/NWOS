package de.nwos.contracts;

/** Ergebnis-Ausgang eines von einem Modul initiierten Sendevorgangs. */
public enum ModuleMessageOutcome {
    DELIVERED,
    DENIED,
    PENDING_USER_CONFIRMATION,
    MODULE_FAILURE
}
