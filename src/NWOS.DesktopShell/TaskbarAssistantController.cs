using System;
using System.Threading.Tasks;

namespace NWOS.DesktopShell
{
    public class TaskbarAssistantController
    {
        private readonly CardinalIpcClient _ipcClient;

        public TaskbarAssistantController(CardinalIpcClient ipcClient)
        {
            _ipcClient = ipcClient ?? throw new ArgumentNullException(nameof(ipcClient));
        }

        /// <summary>
        /// Verarbeitet die Eingabe aus dem universellen Marc/Alya-Textfeld in der Taskleiste.
        /// </summary>
        public async Task<string> HandleUserCommandAsync(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "Bitte gib einen Befehl ein.";

            // Beispiel: Wenn der Nutzer nach Tasks fragt oder Alya ruft
            if (input.StartsWith("/alya", StringComparison.OrdinalIgnoreCase))
            {
                return await _ipcClient.GetAlyaTasksAsync();
            }
            // Beispiel: Widgets auflisten über Marc
            else if (input.StartsWith("/widgets", StringComparison.OrdinalIgnoreCase))
            {
                return await _ipcClient.ListWidgetsAsync();
            }
            // Standard-KI/Assistenz-Anfrage an Marc oder Alya
            else
            {
                // Hier greift später die universelle Verarbeitung
                return $"[Marc/Alya] Verarbeite Anfrage: '{input}'";
            }
        }

        /// <summary>
        /// Lädt die Liste der verfügbaren Widgets für das aufklappbare Mini-Fenster in Bereich 1.
        /// </summary>
        public async Task<string> LoadAvailableWidgetsAsync()
        {
            return await _ipcClient.ListWidgetsAsync();
        }
    }
}