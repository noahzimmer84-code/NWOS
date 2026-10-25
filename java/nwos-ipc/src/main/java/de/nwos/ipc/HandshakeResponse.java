package de.nwos.ipc;

/** Antwort des Servers auf den Identitäts-Handshake. */
public record HandshakeResponse(boolean accepted, String reason) {

    public static HandshakeResponse ok() {
        return new HandshakeResponse(true, null);
    }

    public static HandshakeResponse rejected(String reason) {
        return new HandshakeResponse(false, reason);
    }
}
