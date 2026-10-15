using System;
using System.Threading.Tasks;

namespace NWOS.Host
{
    public class Program
    {
        public static async Task Main(string[] args)
    {
        Console.WriteLine("[NWOS Host] Starte System-Host-Komponente...");

        // Cardinal Server & IPC-Dienste initialisieren
        // Hier greift die Steuerung für den Hintergrundbetrieb

        Console.WriteLine("[NWOS Host] Host läuft stabil. Warte auf UI-Verbindungen...");
        await Task.Delay(-1);
    }
    }
}