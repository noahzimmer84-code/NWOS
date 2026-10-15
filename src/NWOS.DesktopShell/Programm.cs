using System;
using System.Threading.Tasks;

namespace NWOS.DesktopShell
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("Desktop-Shell startet...");

            // IPC-Client initialisieren
            var client = new CardinalIpcClient();

            // 1. Prüfen, ob Alya bereit ist und Tasks abrufen
            string tasksJson = await client.GetAlyaTasksAsync();
            Console.WriteLine("Alya Tasks: " + tasksJson);

            // 2. Einen Task (z.B. Bereinigung beim Start) triggern
            string result = await client.RunAlyaTaskAsync("task-cleanup");
            Console.WriteLine("Task Start Ergebnis: " + result);

            // Hier würde danach z.B. das UI-Fenster geöffnet werden
        }
    }
}