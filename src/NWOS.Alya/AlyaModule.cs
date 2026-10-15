using System.Text.Json;
using NWOS.Contracts;
using NWOS.Settings;

namespace NWOS.Alya;

/// <summary>
/// Registriert sich bei Cardinal als "nwos.alya" (Architekturdokument
/// Abschnitt 10). Besitzt bewusst keinerlei Dateisystem-/Prozess-/Netzwerk-
/// Fähigkeiten (siehe NWOS.Alya.csproj) - alles, was über reinen Dialog
/// hinausgeht, läuft über <see cref="IModuleMessenger"/> an andere Module.
///
/// Unterstützte MessageType-Werte:
///   - "alya.greet"          (kein Payload nötig)
///   - "alya.statusReport"   (kein Payload nötig, Abschnitt 12)
///   - "alya.setTone"        { "tone": "Sachlich" | "Freundlich" }
///   - "alya.getTone"        (kein Payload nötig)
/// </summary>
public sealed class AlyaModule : IModule
{
    public string ModuleId => "nwos.alya";
    public string DisplayName => "Alya";

    private readonly SettingsStore _settings;
    private readonly GreetingBuilder _greetingBuilder = new();
    private readonly StatusReportBuilder _statusReportBuilder;

    private bool _started;

    public AlyaModule(SettingsStore settings, IModuleMessenger? messenger = null)
    {
        _settings = settings;
        _statusReportBuilder = new StatusReportBuilder(messenger, ModuleId);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _started = true;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _started = false;
        return Task.CompletedTask;
    }

    public Task<ModuleHealth> CheckHealthAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new ModuleHealth(ModuleId, _started ? ModuleStatus.Healthy : ModuleStatus.Unresponsive));

    public async Task<ModuleMessage?> HandleMessageAsync(ModuleMessage message, CancellationToken cancellationToken)
    {
        object result = message.MessageType switch
        {
            "alya.greet" => new { text = _greetingBuilder.BuildGreeting(CurrentTone()) },
            "alya.statusReport" => await _statusReportBuilder.BuildAsync(cancellationToken),
            "alya.setTone" => HandleSetTone(message.PayloadJson),
            "alya.getTone" => new { tone = CurrentTone().ToString() },
            _ => throw new NotSupportedException($"Unbekannter MessageType '{message.MessageType}' für {ModuleId}."),
        };

        return message with
        {
            MessageId = Guid.NewGuid().ToString("N"),
            SourceModuleId = ModuleId,
            TargetModuleId = message.SourceModuleId,
            MessageType = $"{message.MessageType}.result",
            PayloadJson = JsonSerializer.Serialize(result, NwosJson.ResponseOptions),
        };
    }

    private AlyaTone CurrentTone()
    {
        var stored = _settings.GetValue(ModuleId, "tone", AlyaTone.Sachlich.ToString());
        return Enum.TryParse<AlyaTone>(stored, out var tone) ? tone : AlyaTone.Sachlich;
    }

    private object HandleSetTone(string payloadJson)
    {
        using var doc = JsonDocument.Parse(payloadJson);
        var toneText = doc.RootElement.GetProperty("tone").GetString()!;

        if (!Enum.TryParse<AlyaTone>(toneText, ignoreCase: true, out var tone))
        {
            throw new ArgumentException($"Unbekannter Ton '{toneText}'. Gültige Werte: {string.Join(", ", Enum.GetNames<AlyaTone>())}.");
        }

        _settings.SetValue(ModuleId, "tone", tone.ToString());
        return new { tone = tone.ToString() };
    }
}
