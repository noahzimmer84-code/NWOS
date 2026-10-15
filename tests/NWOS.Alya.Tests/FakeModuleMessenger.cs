using NWOS.Contracts;

namespace NWOS.Alya.Tests;

/// <summary>
/// Simuliert, wie Cardinal eine von Alya initiierte Nachricht an ein anderes
/// Modul weiterreichen würde - ohne den echten NWOS.Cardinal-Prozess im
/// Test aufzubauen. Erlaubt gezielt "Delivered", "Denied" oder
/// "ModuleFailure" zu simulieren, um zu prüfen, dass StatusReportBuilder mit
/// allen drei Fällen sinnvoll umgeht statt bei einem Fehlerfall abzustürzen.
/// </summary>
internal sealed class FakeModuleMessenger : IModuleMessenger
{
    public ModuleMessageOutcome OutcomeToReturn { get; set; } = ModuleMessageOutcome.Delivered;
    public ModuleMessage? ResponseToReturn { get; set; }
    public ModuleMessage? LastSentMessage { get; private set; }

    public Task<ModuleMessageResult> SendAsync(ModuleMessage message, CancellationToken cancellationToken)
    {
        LastSentMessage = message;
        return Task.FromResult(new ModuleMessageResult(OutcomeToReturn, ResponseToReturn, "simuliert"));
    }
}
