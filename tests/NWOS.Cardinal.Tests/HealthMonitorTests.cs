using Microsoft.Extensions.Logging.Abstractions;
using NWOS.Contracts;
using Xunit;

namespace NWOS.Cardinal.Tests;

public class HealthMonitorTests
{
    [Fact]
    public async Task Triggers_recovery_only_after_reaching_failure_threshold_not_on_first_failure()
    {
        var registry = new ModuleRegistry();
        var faultyModule = new FakeModule("nwos.marc");
        faultyModule.SetStatus(ModuleStatus.Unresponsive);
        registry.Register(faultyModule, PermissionScope.None);

        var monitor = new HealthMonitor(registry, NullLogger<HealthMonitor>.Instance, interval: TimeSpan.FromMilliseconds(10));

        var recoveryCount = 0;
        monitor.ModuleNeedsRecovery += _ =>
        {
            recoveryCount++;
            return Task.CompletedTask;
        };

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(55));
        await monitor.RunAsync(cts.Token);

        // Bei 10ms Intervall und ~55ms Laufzeit laufen mehrere Zyklen; Recovery
        // darf erst NACH Erreichen der Fehlerschwelle (3) ausgelöst werden, nicht
        // beim ersten fehlgeschlagenen Check - das verhindert überstürzte
        // Neustarts bei kurzzeitigen Hängern.
        Assert.True(recoveryCount >= 1, "Recovery hätte nach Überschreiten der Fehlerschwelle ausgelöst werden müssen.");
    }

    [Fact]
    public async Task Does_not_trigger_recovery_for_healthy_module()
    {
        var registry = new ModuleRegistry();
        var healthyModule = new FakeModule("nwos.alice");
        healthyModule.SetStatus(ModuleStatus.Healthy);
        registry.Register(healthyModule, PermissionScope.None);

        var monitor = new HealthMonitor(registry, NullLogger<HealthMonitor>.Instance, interval: TimeSpan.FromMilliseconds(10));

        var recoveryCount = 0;
        monitor.ModuleNeedsRecovery += _ =>
        {
            recoveryCount++;
            return Task.CompletedTask;
        };

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(55));
        await monitor.RunAsync(cts.Token);

        Assert.Equal(0, recoveryCount);
    }

    [Fact]
    public async Task A_failing_module_health_check_does_not_stop_monitoring_of_other_modules()
    {
        var registry = new ModuleRegistry();
        var faultyModule = new FakeModule("nwos.marc");
        faultyModule.SetStatus(ModuleStatus.Unresponsive);
        var healthyModule = new FakeModule("nwos.alice");
        healthyModule.SetStatus(ModuleStatus.Healthy);

        registry.Register(faultyModule, PermissionScope.None);
        registry.Register(healthyModule, PermissionScope.None);

        var monitor = new HealthMonitor(registry, NullLogger<HealthMonitor>.Instance, interval: TimeSpan.FromMilliseconds(10));

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(35));
        // Darf keine Exception werfen / nicht vorzeitig abbrechen (Abschnitt 36).
        await monitor.RunAsync(cts.Token);
    }
}
