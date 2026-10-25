package de.nwos.host;

import de.nwos.cardinal.CardinalService;
import de.nwos.cardinal.HealthMonitor;
import de.nwos.cardinal.ModuleRegistry;
import de.nwos.cardinal.PermissionManager;
import de.nwos.cardinal.TaskRouter;
import de.nwos.contracts.PermissionScope;
import de.nwos.ipc.IpcDefaults;
import de.nwos.ipc.IpcServer;

import java.time.Duration;
import java.util.EnumSet;
import java.util.concurrent.CountDownLatch;

/**
 * Echte Einstiegspunkt-Implementierung des NWOS-Hosts (PT1-Fix: die
 * C#-Einstiegspunkte waren Stubs, die die HostComposition nie starteten -
 * hier läuft der komplette Bootstrap: Registry -> Permissions -> Router ->
 * Cardinal -> IPC-Bus).
 *
 * <p>Start: {@code java -jar nwos-host.jar [port]}</p>
 */
public final class Main {

    public static void main(String[] args) throws Exception {
        int port = args.length > 0
                ? Integer.parseInt(args[0])
                : IpcDefaults.DEFAULT_PORT;

        var registry = new ModuleRegistry();
        var permissions = new PermissionManager(registry);
        var router = new TaskRouter(registry, permissions);
        var healthMonitor = new HealthMonitor(registry, Duration.ofSeconds(30));
        var cardinal = new CardinalService(registry, router, healthMonitor);

        // Modul-Registrierung durch den Host anhand der Konfiguration
        // (Abschnitt 4) - Module können sich nicht selbst Rechte geben.
        registry.register(new SystemModule(), EnumSet.noneOf(PermissionScope.class));

        // Externe Identität des Overlays (liest Status ab).
        registry.grantExternalScope(IpcDefaults.EXTERNAL_OVERLAY_IDENTITY,
                EnumSet.of(PermissionScope.FILE_SYSTEM_READ));

        var server = new IpcServer(router, registry, port);
        server.start();
        cardinal.start();

        Runtime.getRuntime().addShutdownHook(new Thread(() -> {
            server.close();
            cardinal.shutdown();
        }, "nwos-shutdown"));

        System.out.println("NWOS Cardinal läuft. IPC-Bus: 127.0.0.1:" + server.port());
        System.out.println("Beenden mit Strg+C.");

        new CountDownLatch(1).await(); // Hält den Haupt-Thread am Leben.
    }

    private Main() {
    }
}
