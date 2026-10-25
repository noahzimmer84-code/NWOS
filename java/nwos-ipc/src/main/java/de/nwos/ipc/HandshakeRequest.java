package de.nwos.ipc;

/**
 * Erste gerahmte Nachricht jeder Verbindung: der Aufrufer identifiziert sich.
 * Der Server prüft die Identität gegen die in der ModuleRegistry vergebenen
 * Grants und lehnt unbekannte Identitäten ab (PT1-Fix: Identity-Enforcement).
 */
public record HandshakeRequest(String identity) {
}
