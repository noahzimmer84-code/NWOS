using System;
using System.IO;
using Microsoft.Win32;

namespace NWOS.Setup;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== NWOS Natives Setup ===");

        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string installDir = Path.Combine(localAppData, "Programs", "NWOS");
        string dataDir = Path.Combine(localAppData, "NWOS");

        Console.WriteLine($"Zielverzeichnis: {installDir}");
        Console.WriteLine($"Datenverzeichnis: {dataDir}");

        try
        {
            // 1. Verzeichnisse erstellen
            Directory.CreateDirectory(installDir);
            Directory.CreateDirectory(dataDir);
            Directory.CreateDirectory(Path.Combine(installDir, "Cardinal"));
            Directory.CreateDirectory(Path.Combine(installDir, "Ui"));

            // 2. Konfiguration anlegen (falls noch nicht vorhanden)
            string configPath = Path.Combine(dataDir, "nwos.config.json");
            if (!File.Exists(configPath))
            {
                string defaultConfig = @"{
  ""EnableDesktopShell"": true,
  ""EnableSmartBoxes"": true,
  ""EnableWidgets"": true,
  ""EnableAlya"": true,
  ""EnableMarc"": true,
  ""EnableAlice"": true,
  ""EnableMultiDevice"": false,
  ""EnableMaintenance"": false,
  ""EnableSystemCenter"": true
}";
                File.WriteAllText(configPath, defaultConfig);
                Console.WriteLine(" Standard-Konfiguration erstellt.");
            }

            // 3. Start-Batch erzeugen
            string launcherPath = Path.Combine(installDir, "Start-NWOS.bat");
            string launcherContent =
                "@echo off\r\n" +
                "tasklist /fi \"imagename eq NWOS.Bootstrap.Windows.exe\" | find /i \"NWOS.Bootstrap.Windows\" >nul || start \"\" /min \"%~dp0Cardinal\\NWOS.Bootstrap.Windows.exe\"\r\n" +
                "timeout /t 3 /nobreak >nul\r\n" +
                "start \"\" \"%~dp0Ui\\NWOS.DesktopShell.Ui.exe\"\r\n";
            File.WriteAllText(launcherPath, launcherContent);

            // 4. Registry: Autostart für den aktuellen Benutzer eintragen
            string cardinalExe = Path.Combine(installDir, "Cardinal", "NWOS.Bootstrap.Windows.exe");
            string runValue = $"cmd.exe /c start \"\" /min \"{cardinalExe}\"";

            using (var runKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
            {
                runKey?.SetValue("NWOS", runValue);
            }
            Console.WriteLine(" Autostart in Registry registriert.");

            // 5. Registry: Deinstallation eintragen
            string uninstallSubKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\NWOS";
            using (var uninstKey = Registry.CurrentUser.CreateSubKey(uninstallSubKey))
            {
                uninstKey?.SetValue("DisplayName", "NWOS - NightWing Operating System");
                uninstKey?.SetValue("DisplayVersion", "0.2.0");
                uninstKey?.SetValue("Publisher", "NWOS");
                uninstKey?.SetValue("InstallLocation", installDir);
                uninstKey?.SetValue("NoModify", 1, RegistryValueKind.DWord);
            }
            Console.WriteLine(" Deinstallations-Eintrag erstellt.");

            Console.WriteLine("\n Installation erfolgreich abgeschlossen!");
        }
        -catch (Exception ex)
        {
            Console.WriteLine($"\n Fehler bei der Installation: {ex.Message}");
        }

        Console.WriteLine("\nDrücke eine beliebige Taste zum Beenden...");
        Console.ReadKey();
    }
}