using System.Diagnostics;
using System.Management;
using System.Runtime.Versioning;

namespace NWOS.SystemCenter.Windows;

/// <summary>
/// Liest echte Systemwerte über WMI (Windows Management Instrumentation) -
/// der Standardweg für genau diese Art von Systeminformationen unter Windows,
/// von vielen etablierten System-Tools genutzt. Kein synthetischer/
/// zufälliger Platzhalterwert (Abschnitt 40: keine Fake-Funktionen).
///
/// TODO(Phase 7): GPU-Auslastung und Temperaturen (Abschnitt 24) fehlen noch -
/// beides ist über WMI nur eingeschränkt/herstellerabhängig verfügbar
/// (Temperaturen z. B. oft nur über herstellerspezifische WMI-Namespaces
/// oder Zusatztreiber). Bewusst weggelassen statt einen falschen Wert
/// vorzutäuschen; <see cref="SystemStatusSnapshot"/> hat entsprechend noch
/// keine Felder dafür - werden ergänzt, sobald eine verlässliche Quelle
/// feststeht.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WmiSystemStatusProvider : ISystemStatusProvider
{
    public Task<SystemStatusSnapshot> CaptureAsync(CancellationToken cancellationToken)
    {
        var cpuUsage = TryGetCpuUsagePercent();
        var (totalMemory, availableMemory) = TryGetMemoryBytes();
        var disks = TryGetDisks();
        var activeProcessCount = Process.GetProcesses().Length;

        return Task.FromResult(new SystemStatusSnapshot(
            cpuUsage, totalMemory, availableMemory, disks, activeProcessCount, DateTimeOffset.UtcNow));
    }

    private static double TryGetCpuUsagePercent()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT LoadPercentage FROM Win32_Processor");
            var values = searcher.Get()
                .Cast<ManagementObject>()
                .Select(mo => Convert.ToDouble(mo["LoadPercentage"] ?? 0))
                .ToList();

            return values.Count > 0 ? values.Average() : 0;
        }
        catch (ManagementException)
        {
            // Abschnitt 36-Grundsatz: WMI kann z. B. per Gruppenrichtlinie
            // deaktiviert sein - dann lieber 0 zurückgeben als abstürzen.
            return 0;
        }
    }

    private static (long TotalBytes, long AvailableBytes) TryGetMemoryBytes()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");

            foreach (ManagementObject mo in searcher.Get())
            {
                var totalKb = Convert.ToInt64(mo["TotalVisibleMemorySize"] ?? 0);
                var freeKb = Convert.ToInt64(mo["FreePhysicalMemory"] ?? 0);
                return (totalKb * 1024, freeKb * 1024);
            }
        }
        catch (ManagementException)
        {
            // siehe TryGetCpuUsagePercent
        }

        return (0, 0);
    }

    private static IReadOnlyList<DiskStatus> TryGetDisks()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT DeviceID, Size, FreeSpace FROM Win32_LogicalDisk WHERE DriveType = 3");

            return searcher.Get()
                .Cast<ManagementObject>()
                .Select(mo => new DiskStatus(
                    (string)mo["DeviceID"],
                    Convert.ToInt64(mo["Size"] ?? 0),
                    Convert.ToInt64(mo["FreeSpace"] ?? 0)))
                .ToList();
        }
        catch (ManagementException)
        {
            return Array.Empty<DiskStatus>();
        }
    }
}
