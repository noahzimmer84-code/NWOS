package de.nwos.contracts;

/**
 * Ergebnis einer Berechtigungsprüfung inkl. Begründung, damit Entscheidungen
 * nachvollziehbar protokolliert werden können (Abschnitt 8: "Sicherheitsrelevante
 * Aktionen müssen nachvollziehbar protokolliert werden").
 */
public record PermissionDecision(boolean allowed, String reason, boolean requiresUserConfirmation) {

    public static PermissionDecision allow(String reason, boolean requiresUserConfirmation) {
        return new PermissionDecision(true, reason, requiresUserConfirmation);
    }

    public static PermissionDecision deny(String reason) {
        return new PermissionDecision(false, reason, false);
    }
}
