package de.nwos.ipc;

import com.fasterxml.jackson.databind.ObjectMapper;
import de.nwos.cardinal.ModuleRegistry;
import de.nwos.cardinal.TaskRouter;
import de.nwos.contracts.ModuleMessage;
import de.nwos.contracts.ModuleMessageOutcome;
import de.nwos.contracts.ModuleMessageResult;
import de.nwos.contracts.NwosJson;

import java.io.BufferedInputStream;
import java.io.BufferedOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.InetAddress;
import java.net.ServerSocket;
import java.net.Socket;
import java.util.Objects;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

/**
 * Framing-basierter IPC-Server des Cardinal-Bus.
 *
 * <p>PT1-Fixes gegenüber der C#-Version:</p>
 * <ul>
 *   <li>Framing: Jede Nachricht (Handshake UND Payload) wird mit
 *       Längenpräfix über {@link FrameCodec} übertragen - der PT1-Server
 *       erwartete ungerahmten JSON-Stream während der Client rahmte.</li>
 *   <li>Identity-Enforcement: Jede Verbindung beginnt mit einem
 *       {@link HandshakeRequest}. Unbekannte Identitäten werden abgelehnt;
 *       jede Nachricht muss als Quelle die verifizierte Identität der
 *       Verbindung angeben - Spoofing wird damit unmöglich.</li>
 *   <li>Mehrere Verbindungen: PT1 bediente genau einen Client
 *       (maxNumberOfServerInstances=1); hier bearbeitet ein
 *       Virtual-Thread-Pro-Verbindung-Executor beliebig viele Clients
 *       parallel.</li>
 * </ul>
 */
public final class IpcServer implements AutoCloseable {

    private static final System.Logger LOG = System.getLogger(IpcServer.class.getName());

    private final TaskRouter router;
    private final ModuleRegistry registry;
    private final int requestedPort;
    private final ObjectMapper mapper = NwosJson.mapper();

    private volatile boolean running;
    private volatile ServerSocket serverSocket;
    private ExecutorService connectionExecutor;
    private Thread acceptThread;

    public IpcServer(TaskRouter router, ModuleRegistry registry, int port) {
        this.router = Objects.requireNonNull(router, "router");
        this.registry = Objects.requireNonNull(registry, "registry");
        this.requestedPort = port;
    }

    /** Öffnet den Bus auf 127.0.0.1 und startet den Accept-Loop. */
    public synchronized void start() throws IOException {
        if (running) {
            return;
        }
        var socket = new ServerSocket(requestedPort, 50, InetAddress.getByName(IpcDefaults.LOOPBACK_HOST));
        connectionExecutor = Executors.newVirtualThreadPerTaskExecutor();
        running = true;
        serverSocket = socket;
        acceptThread = Thread.ofPlatform().name("nwos-ipc-accept").start(this::acceptLoop);
        LOG.log(System.Logger.Level.INFO, "IPC-Server lauscht auf {0}:{1}.",
                IpcDefaults.LOOPBACK_HOST, socket.getLocalPort());
    }

    /** Tatsächlich gebundener Port (relevant bei ephemerem Port 0). */
    public int port() {
        var socket = serverSocket;
        return socket == null ? -1 : socket.getLocalPort();
    }

    private void acceptLoop() {
        while (running) {
            try {
                var client = serverSocket.accept();
                connectionExecutor.submit(() -> handleConnection(client));
            } catch (IOException ex) {
                if (running) {
                    LOG.log(System.Logger.Level.ERROR, "Fehler im IPC-Accept-Loop: {0}", ex.getMessage());
                }
            }
        }
    }

    private void handleConnection(Socket socket) {
        try (socket;
             InputStream in = new BufferedInputStream(socket.getInputStream());
             OutputStream out = new BufferedOutputStream(socket.getOutputStream())) {

            var handshake = mapper.readValue(FrameCodec.readFrame(in), HandshakeRequest.class);
            if (handshake == null || !registry.hasGrantedIdentity(handshake.identity())) {
                LOG.log(System.Logger.Level.WARNING, "IPC-Verbindung mit unbekannter Identität abgewiesen.");
                FrameCodec.writeFrame(out, mapper.writeValueAsBytes(
                        HandshakeResponse.rejected("Unbekannte Identität - Zugriff verweigert.")));
                return;
            }
            FrameCodec.writeFrame(out, mapper.writeValueAsBytes(HandshakeResponse.ok()));

            while (running) {
                byte[] frame;
                try {
                    frame = FrameCodec.readFrame(in);
                } catch (IOException endOfStream) {
                    return; // Client hat die Verbindung beendet - normaler Fall.
                }

                var request = mapper.readValue(frame, ModuleMessage.class);
                var result = enforceIdentityAndRoute(handshake.identity(), request);
                FrameCodec.writeFrame(out, mapper.writeValueAsBytes(result));
            }
        } catch (Exception ex) {
            LOG.log(System.Logger.Level.ERROR, "Fehler beim Verarbeiten einer IPC-Verbindung: {0}", ex.getMessage());
        }
    }

    /**
     * Kernstück des Identity-Enforcements: die Quelle der Nachricht MUSS der
     * verifizierten Identität der Verbindung entsprechen. Ein kompromittierter
     * Client kann sich thus nicht als anderes Modul ausgeben.
     */
    private ModuleMessageResult enforceIdentityAndRoute(String verifiedIdentity, ModuleMessage request) {
        if (request == null || !verifiedIdentity.equals(request.sourceModuleId())) {
            LOG.log(System.Logger.Level.WARNING,
                    "Identitäts-Spoofing erkannt: Verbindung gehört ''{0}'', Nachricht behauptet ''{1}''.",
                    verifiedIdentity, request == null ? "<null>" : request.sourceModuleId());
            return ModuleMessageResult.denied(
                    "Identitäts-Spoofing erkannt: Verbindung gehört '" + verifiedIdentity
                            + "', Nachricht behauptet '" + (request == null ? "<null>" : request.sourceModuleId()) + "'.");
        }

        var routing = router.route(request).join();
        return new ModuleMessageResult(
                switch (routing.outcome()) {
                    case DELIVERED -> ModuleMessageOutcome.DELIVERED;
                    case DENIED -> ModuleMessageOutcome.DENIED;
                    case PENDING_USER_CONFIRMATION -> ModuleMessageOutcome.PENDING_USER_CONFIRMATION;
                    case MODULE_FAILURE -> ModuleMessageOutcome.MODULE_FAILURE;
                },
                routing.response(), routing.reason());
    }

    @Override
    public synchronized void close() {
        running = false;
        try {
            if (serverSocket != null) {
                serverSocket.close();
            }
        } catch (IOException ignored) {
        }
        if (connectionExecutor != null) {
            connectionExecutor.shutdownNow();
        }
    }
}
