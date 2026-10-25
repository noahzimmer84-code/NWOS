package de.nwos.ipc;

import com.fasterxml.jackson.databind.ObjectMapper;
import de.nwos.contracts.ModuleMessage;
import de.nwos.contracts.ModuleMessageResult;
import de.nwos.contracts.NwosJson;

import java.io.BufferedInputStream;
import java.io.BufferedOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.InetSocketAddress;
import java.net.Socket;
import java.time.Duration;
import java.util.Objects;

/**
 * Client-Gegenstück zum {@link IpcServer}. Meldet sich beim Verbinden mit
 * seiner Identität an (Handshake) und überträgt danach ausschließlich
 * gerahmte Nachrichten - dasselbe Framing wie der Server (PT1-Fix).
 *
 * <p>Nicht threadsicher: eine Instanz, ein Socket, ein Aufrufer.
 * Für parallele Aufrufer mehrere Instanzen verwenden.</p>
 */
public final class IpcClient implements AutoCloseable {

    private static final System.Logger LOG = System.getLogger(IpcClient.class.getName());

    private final String identity;
    private final String host;
    private final int port;
    private final Duration connectTimeout;
    private final ObjectMapper mapper = NwosJson.mapper();

    private Socket socket;
    private InputStream in;
    private OutputStream out;
    private boolean open;

    public IpcClient(String identity, String host, int port, Duration connectTimeout) {
        this.identity = Objects.requireNonNull(identity, "identity");
        this.host = host == null ? IpcDefaults.LOOPBACK_HOST : host;
        this.port = port;
        this.connectTimeout = connectTimeout == null ? Duration.ofSeconds(5) : connectTimeout;
    }

    public IpcClient(String identity, int port) {
        this(identity, IpcDefaults.LOOPBACK_HOST, port, null);
    }

    public IpcClient(String identity, int port, Duration connectTimeout) {
        this(identity, IpcDefaults.LOOPBACK_HOST, port, connectTimeout);
    }

    /** Baut die Verbindung auf und führt den Identitäts-Handshake durch. */
    public synchronized void connect() throws IOException {
        if (open) {
            return;
        }
        socket = new Socket();
        try {
            socket.connect(new InetSocketAddress(host, port), (int) connectTimeout.toMillis());
            socket.setSoTimeout((int) connectTimeout.toMillis());
        } catch (IOException ex) {
            close();
            throw new IOException("Konnte innerhalb von " + connectTimeout.toSeconds()
                    + "s keine Verbindung zu Cardinal (" + host + ":" + port + ") herstellen - läuft der NWOS-Prozess?");
        }

        in = new BufferedInputStream(socket.getInputStream());
        out = new BufferedOutputStream(socket.getOutputStream());

        FrameCodec.writeFrame(out, mapper.writeValueAsBytes(new HandshakeRequest(identity)));
        var response = mapper.readValue(FrameCodec.readFrame(in), HandshakeResponse.class);
        if (!response.accepted()) {
            close();
            throw new IOException("IPC-Handshake abgelehnt: " + response.reason());
        }
        open = true;
        LOG.log(System.Logger.Level.DEBUG, "IPC-Client ''{0}'' verbunden mit {1}:{2}.", identity, host, port);
    }

    /** Sendet eine Nachricht und wartet auf das framing-gerechte Ergebnis. */
    public synchronized ModuleMessageResult send(ModuleMessage message) throws IOException {
        if (!open) {
            throw new IllegalStateException("Client ist nicht verbunden - zuerst connect() aufrufen.");
        }
        FrameCodec.writeFrame(out, mapper.writeValueAsBytes(message));
        return mapper.readValue(FrameCodec.readFrame(in), ModuleMessageResult.class);
    }

    @Override
    public synchronized void close() {
        open = false;
        if (socket != null) {
            try {
                socket.close();
            } catch (IOException ignored) {
            }
            socket = null;
            in = null;
            out = null;
        }
    }
}
