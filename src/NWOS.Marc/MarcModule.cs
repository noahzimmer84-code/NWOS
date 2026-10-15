using System.Text.Json;
using NWOS.Contracts;

namespace NWOS.Marc;

/// <summary>
/// Registriert sich bei Cardinal als "nwos.marc" (Architekturdokument
/// Abschnitt 9). Führt ausschließlich lokale Aufgaben aus, keine wie auch
/// immer geartete Netzwerkaktion (siehe Begründung in NWOS.Marc.csproj).
///
/// Unterstützte MessageType-Werte:
///   - "marc.createFolder"     { "path": string }
///   - "marc.renameFile"       { "filePath": string, "newFileName": string }
///   - "marc.organizeFolder"   { "folderPath": string }
///   - "marc.launchApp"        { "executablePath": string, "arguments"?: string }
///   - "marc.closeApp"         { "processId": int }
/// </summary>
public sealed class MarcModule : IModule
{
    public string ModuleId => "nwos.marc";
    public string DisplayName => "Marc";

    private readonly FileOrganizer _organizer = new();
    private readonly FileTaskExecutor _fileTasks = new();
    private readonly ProcessController _processController = new();

    private bool _started;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Marc hält keinen eigenen Hintergrundzustand (im Gegensatz zu Alice's
        // FileSystemWatcher) - er reagiert ausschließlich auf eingehende
        // Nachrichten. StartAsync/StopAsync existieren trotzdem, damit Marc
        // sich wie jedes andere Modul einheitlich über den Lebenszyklus
        // verhält (Abschnitt 36: einheitliche Modul-Schnittstelle).
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
        using var payload = JsonDocument.Parse(message.PayloadJson);
        var root = payload.RootElement;

        object result = message.MessageType switch
        {
            "marc.createFolder" => HandleCreateFolder(root),
            "marc.renameFile" => HandleRenameFile(root),
            "marc.organizeFolder" => HandleOrganizeFolder(root),
            "marc.launchApp" => HandleLaunchApp(root),
            "marc.closeApp" => HandleCloseApp(root),
            _ => throw new NotSupportedException($"Unbekannter MessageType '{message.MessageType}' für {ModuleId}."),
        };

        await Task.CompletedTask; // Platzhalter für zukünftige async Dateioperationen; aktuell alles synchron.

        return message with
        {
            MessageId = Guid.NewGuid().ToString("N"),
            SourceModuleId = ModuleId,
            TargetModuleId = message.SourceModuleId,
            MessageType = $"{message.MessageType}.result",
            PayloadJson = JsonSerializer.Serialize(result, NwosJson.ResponseOptions),
        };
    }

    private object HandleCreateFolder(JsonElement root)
    {
        var path = root.GetProperty("path").GetString()!;
        _fileTasks.CreateFolder(path);
        return new { created = path };
    }

    private object HandleRenameFile(JsonElement root)
    {
        var filePath = root.GetProperty("filePath").GetString()!;
        var newFileName = root.GetProperty("newFileName").GetString()!;
        var newPath = _fileTasks.RenameFile(filePath, newFileName);
        return new { newPath };
    }

    private object HandleOrganizeFolder(JsonElement root)
    {
        var folderPath = root.GetProperty("folderPath").GetString()!;
        var result = _organizer.Organize(folderPath);
        return result;
    }

    private object HandleLaunchApp(JsonElement root)
    {
        var executablePath = root.GetProperty("executablePath").GetString()!;
        var arguments = root.TryGetProperty("arguments", out var a) ? a.GetString() ?? "" : "";
        var processId = _processController.LaunchApplication(executablePath, arguments);
        return new { processId };
    }

    private object HandleCloseApp(JsonElement root)
    {
        var processId = root.GetProperty("processId").GetInt32();
        var closed = _processController.CloseApplication(processId);
        return new { closed };
    }
}
