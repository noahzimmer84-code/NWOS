using NWOS.Contracts;

namespace NWOS.Cardinal.Tests;

/// <summary>
/// Steuerbares Test-Modul: simuliert normales Verhalten, Degradation, Absturz
/// (nicht reagierend) und optional werfende Handler - genau die Fälle, die
/// laut Abschnitt 36 isoliert bleiben müssen.
/// </summary>
internal sealed class FakeModule : IModule
{
    public string ModuleId { get; }
    public string DisplayName => ModuleId;

    public int StartCount { get; private set; }
    public int StopCount { get; private set; }
    public bool ThrowOnHandleMessage { get; set; }

    private ModuleStatus _status = ModuleStatus.Healthy;

    public FakeModule(string moduleId) => ModuleId = moduleId;

    public void SetStatus(ModuleStatus status) => _status = status;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        StartCount++;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        StopCount++;
        return Task.CompletedTask;
    }

    public Task<ModuleHealth> CheckHealthAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new ModuleHealth(ModuleId, _status));

    public Task<ModuleMessage?> HandleMessageAsync(ModuleMessage message, CancellationToken cancellationToken)
    {
        if (ThrowOnHandleMessage)
        {
            throw new InvalidOperationException("Simulierter Modulfehler.");
        }

        return Task.FromResult<ModuleMessage?>(message with { MessageType = "ack" });
    }
}
