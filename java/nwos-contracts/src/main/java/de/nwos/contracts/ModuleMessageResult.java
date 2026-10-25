package de.nwos.contracts;

/**
 * Ergebnis eines Sendevorgangs. Bewusst unabhängig vom Routing-Ergebnis des
 * Cardinal definiert, damit die Contracts (die auch von Modul-Projekten
 * referenziert werden) keine Abhängigkeit auf Cardinal brauchen - das würde
 * einen Zirkelbezug erzeugen.
 */
public record ModuleMessageResult(ModuleMessageOutcome outcome, ModuleMessage response, String reason) {

    public static ModuleMessageResult delivered(ModuleMessage response) {
        return new ModuleMessageResult(ModuleMessageOutcome.DELIVERED, response, null);
    }

    public static ModuleMessageResult denied(String reason) {
        return new ModuleMessageResult(ModuleMessageOutcome.DENIED, null, reason);
    }

    public static ModuleMessageResult pendingUserConfirmation(String reason) {
        return new ModuleMessageResult(ModuleMessageOutcome.PENDING_USER_CONFIRMATION, null, reason);
    }

    public static ModuleMessageResult moduleFailure(String reason) {
        return new ModuleMessageResult(ModuleMessageOutcome.MODULE_FAILURE, null, reason);
    }
}
