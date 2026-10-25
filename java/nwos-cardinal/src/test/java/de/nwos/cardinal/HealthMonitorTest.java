package de.nwos.cardinal;

import de.nwos.contracts.IModule;
import de.nwos.contracts.ModuleHealth;
import de.nwos.contracts.ModuleMessage;
import de.nwos.contracts.ModuleStatus;
import org.junit.jupiter.api.Test;

import java.time.Duration;
import java.util.Set;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicInteger;

import static org.junit.jupiter.api.Assertions.assertTrue;

class HealthMonitorTest {

    @Test
    void triggersRecoveryAfterThreeConsecutiveFailures() throws Exception {
        var registry = new ModuleRegistry();
        var flaky = new FlakyModule("nwos.flaky");
        registry.register(flaky, Set.of());

        var monitor = new HealthMonitor(registry, Duration.ofMillis(5));
        var recoveryCalled = new CountDownLatch(1);
        monitor.setRecoveryHandler(module -> {
            recoveryCalled.countDown();
            return CompletableFuture.completedFuture(null);
        });

        monitor.start();
        assertTrue(recoveryCalled.await(5, TimeUnit.SECONDS),
                "Recovery hätte nach 3 aufeinanderfolgenden Fehlern ausgelöst werden müssen.");
        assertTrue(flaky.healthChecks.get() >= HealthMonitor.MAX_CONSECUTIVE_FAILURES_BEFORE_RECOVERY);
        monitor.stop();
    }

    /** Liefert beim Health-Check konstant UNRESPONSIVE. */
    private static final class FlakyModule implements IModule {
        final AtomicInteger healthChecks = new AtomicInteger();
        private final String id;

        private FlakyModule(String id) {
            this.id = id;
        }

        @Override public String getModuleId() { return id; }
        @Override public String getDisplayName() { return id; }
        @Override public CompletableFuture<Void> start() { return CompletableFuture.completedFuture(null); }
        @Override public CompletableFuture<Void> stop() { return CompletableFuture.completedFuture(null); }
        @Override public CompletableFuture<ModuleHealth> checkHealth() {
            healthChecks.incrementAndGet();
            return CompletableFuture.completedFuture(ModuleHealth.of(id, ModuleStatus.UNRESPONSIVE, "Test: nicht erreichbar."));
        }
        @Override public CompletableFuture<ModuleMessage> handleMessage(ModuleMessage message) {
            return CompletableFuture.completedFuture(null);
        }
    }
}
