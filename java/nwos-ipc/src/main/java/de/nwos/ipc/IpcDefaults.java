package de.nwos.ipc;

/**
 * Zentrale IPC-Konstanten. Port von NWOS.Ipc.IpcDefaults.
 *
 * <p>Transport-Entscheidung: Java kennt keine benannten Pipes (Named Pipes)
 * auf nativer Ebene. Statt einer Windows-only-JNA-Bridge nutzt die
 * Java-Version einen Loopback-Socket, der nur auf 127.0.0.1 gebunden wird -
 * plattformunabhängig, ohne externe Ports, und in der Sandbox testbar. Die
 * Absicherung läuft über den Identitäts-Handshake; ein Windows-Named-Pipe-
 * Transport kann später als weitere Implementierung ergänzt werden.</p>
 */
public final class IpcDefaults {

    /** Bindet nur auf 127.0.0.1 - kein externer Zugriff. */
    public static final String LOOPBACK_HOST = "127.0.0.1";

    /** Standardport des Cardinal-IPC-Bus. */
    public static final int DEFAULT_PORT = 45110;

    /** Standardport für Tests / flüchtige Instanzen (ephemer). */
    public static final int EPHEMERAL_PORT = 0;

    public static final String EXTERNAL_OVERLAY_IDENTITY = "nwos.overlay";

    private IpcDefaults() {
    }
}
