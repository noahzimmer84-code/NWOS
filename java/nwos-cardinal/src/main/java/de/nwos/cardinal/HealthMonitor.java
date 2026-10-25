package de.nwos.cardinal;

import de.nwos.contracts.IModule;
import de.nwos.contracts.ModuleHealth;
import de.nwos.contracts.ModuleStatus;

import java.time.Duration;
import java.util.Map;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.Executors;
import java.util.concurrent.ScheduledExecutorService;
import java.util.concurrent.TimeUnit;
import java.util.function.Function;

/**
 * Überwacht alle registrierten Module per Heartbeat (Architekturdokument
 * Abschnitt 5 / 36: Fehler eines Moduls dürfen nicht das Gesamtsystem
 * mitreißen). Bewusst intervallbasiert statt eng getaktetem Polling
 * (Abschnitt 35: "keine unnötigen Polling-Schleifen") - das Intervall ist
 * konfigurierbar und großzügig bemessen.
 *
 * <p>Port von NWOS.Cardinal.HealthMonitor.</p>
 */
public final class HealthMonitor {

    static final int MAX_CONSECUTIVE_FAILURES_BEFORE_RECOVERY = 3;

    private static final System.Logger LOG = System.getLogger(HealthMonitor.class.getName());

    private final ModuleRegistry registry;
    private final Duration interval;
    private final Map<String, Integer> consecutiveFailures = new ConcurrentHashMap<>();
    private final ScheduledExecutorService scheduler = Executors.newSingleThreadScheduledExecutor(runnable -> {
        var thread = new Thread(runnable, "nwos-health-monitor");
        thread.setDaemon(true);
        return thread;
    });

    private volatile Function<IModule, CompletableFuture<Void>> recoveryHandler;
    private volatile boolean running;

    public HealthMonitor(ModuleRegistry registry, Duration interval) {
        this.registry = registry;
        this.interval = interval == null ? Duration.ofSeconds(30) : interval;
    }

    /**
     * Callback, der greift, wenn ein Modul die Fehlerschwelle überschreitet
     * (Recovery statt endlosem Neuversuch - verhindert Crash-Loops).
     */
    public void setRecoveryHandler(Function<IModule, CompletableFuture<Void>> handler) {
        this.recoveryHandler = handler;
    }

    public synchronized void start() {
        if (running) {
            return;
        }
        running = true;
        scheduler.scheduleWithFixedDelay(this::checkAllModules,
                0, interval.toMillis(), TimeUnit.MILLISECONDS);
    }

    public synchronized void stop() {
        running = false;
        scheduler.shutdownNow();
    }

    private void checkAllModules() {
        if (!running) {
            return;
        }
        for (IModule module : registry.allModules()) {
            try {
                checkOneModule(module);
            } catch (Exception ex) {
                // Der Health-Check eines Moduls darf den Monitor nie mitreißen.
                LOG.log(System.Logger.Level.WARNING, "Health-Check für {0} fehlgeschlagen: {1}",
                        module.getModuleId(), ex.getMessage());
            }
        }
    }

    private void checkOneModule(IModule module) {
        ModuleHealth health;
        try {
            health = module.checkHealth().join();
        } catch (Exception ex) {
            // Ein Modul, dessen Health-Check selbst wirft, gilt als nicht
            // reagierend - genau der Fall, den Abschnitt 36 adressiert.
            health = ModuleHealth.of(module.getModuleId(), ModuleStatus.UNRESPONSIVE, ex.getMessage());
        }

        if (health.status() == ModuleStatus.UNRESPONSIVE) {
            int failures = consecutiveFailures.merge(module.getModuleId(), 1, Integer::sum);
            LOG.log(System.Logger.Level.WARNING, "Modul {0} nicht erreichbar ({1}/{2}).",
                    module.getModuleId(), failures, MAX_CONSECUTIVE_FAILURES_BEFORE_RECOVERY);

            var handler = recoveryHandler;
            if (failures >= MAX_CONSECUTIVE_FAILURES_BEFORE_RECOVERY && handler != null) {
                LOG.log(System.Logger.Level.ERROR,
                        "Modul {0} überschreitet Fehlerschwelle - Recovery wird ausgelöst. Andere Module bleiben unberührt.",
                        module.getModuleId());
                try {
                    handler.apply(module).join();
                } catch (Exception ex) {
                    LOG.log(System.Logger.Level.ERROR, "Recovery für {0} fehlgeschlagen: {1}",
                            module.getModuleId(), ex.getMessage());
                }
                consecutiveFailures.put(module.getModuleId(), 0);
            }
        } else {
            consecutiveFailures.put(module.getModuleId(), 0);
        }
    }
}
