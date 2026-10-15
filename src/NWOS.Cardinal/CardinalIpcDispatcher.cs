using System;
using System.Text.Json;
using System.Threading.Tasks;
using NWOS.Alya;
using NWOS.Marc;
using NWOS.Alice;

namespace NWOS.Cardinal
{
    public class CardinalIpcDispatcher
    {
        /// <summary>
        /// Parst den JSON-Request und leitet ihn an das entsprechende Zielmodul weiter.
        /// Kompatibel mit dem Format: {"target":"...","action":"...","payload":"..."}
        /// </summary>
        public async Task<string> DispatchAndExecuteAsync(string requestJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(requestJson);
            var root = doc.RootElement;

            // Unterstützt sowohl "target" (aus CardinalIpcClient) als auch "Service" als Fallback
            string target = root.TryGetProperty("target", out var tProp) ? tProp.GetString() :
                    (root.TryGetProperty("Service", out var sProp) ? sProp.GetString() : string.Empty);

            string action = root.TryGetProperty("action", out var aProp) ? aProp.GetString() :
                    (root.TryGetProperty("Action", out var acProp) ? acProp.GetString() : string.Empty);

            string payload = root.TryGetProperty("payload", out var pProp) ? pProp.ToString() :
                    (root.TryGetProperty("Payload", out var plProp) ? plProp.ToString() : string.Empty);

            if (string.IsNullOrEmpty(target))
            {
                return "{\"status\":\"error\",\"message\":\"Kein Zielmodul (target) angegeben.\"}";
            }

            return target.ToLowerInvariant() switch
            {
                "nwos.alya" => await HandleAlyaRequestAsync(action, payload),
                    "nwos.marc" => await HandleMarcRequestAsync(action, payload),
                    "nwos.alice" => await HandleAliceRequestAsync(action, payload),
                    "nwos.systemcenter" => await HandleSystemCenterRequestAsync(action, payload),
                    _ => $"{{\"status\":\"error\",\"message\":\"Unbekanntes Zielmodul: {target}\"}}"
            };
        }
        catch (Exception ex)
        {
            string safeMessage = ex.Message.Replace("\"", "\\\"");
            return $"{{\"status\":\"error\",\"message\":\"Dispatcher-Parse-Fehler: {safeMessage}\"}}";
        }
    }

        private async Task<string> HandleAlyaRequestAsync(string action, string payload)
    {
        await Task.Delay(10);
        if (action == "system.updatelog")
        {
            return "----------------------------------------\n" +
                    " NWOS System Update-Log (v3.0)\n" +
                    "----------------------------------------\n" +
                    "• Cardinal Kernel: IPC-Named-Pipe aktiv\n" +
                    "• Taskleiste: 5-Zonen-AppBar verankert\n" +
                    "• Alya & Marc: Bereit für Anfragen\n" +
                    "----------------------------------------";
        }
        if (action == "assistant.query")
        {
            return $"[Alya AI] Antwort auf '{payload}': Befehl im Kernel verarbeitet.";
        }
        return $"[Alya] Befehl '{action}' mit Payload '{payload}' verarbeitet.";
    }

        private async Task<string> HandleMarcRequestAsync(string action, string payload)
    {
        await Task.Delay(10);
        return $"[Marc] Prozess-Aktion '{action}' ausgeführt.";
    }

        private async Task<string> HandleAliceRequestAsync(string action, string payload)
    {
        await Task.Delay(10);
        return $"[Alice] Suchergebnis für '{payload}': 3 System-Treffer gefunden.";
    }

        private async Task<string> HandleSystemCenterRequestAsync(string action, string payload)
    {
        await Task.Delay(10);
        return "CPU: 4% | RAM: 1.8 GB / 16 GB";
    }
    }
}