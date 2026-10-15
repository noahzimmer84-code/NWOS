namespace NWOS.SystemCenter;

/// <summary>Momentaufnahme des Systemzustands (Architekturdokument Abschnitt 24).</summary>
public sealed record SystemStatusSnapshot(
    double CpuUsagePercent,
    long TotalMemoryBytes,
    long AvailableMemoryBytes,
    IReadOnlyList<DiskStatus> Disks,
    int ActiveProcessCount,
    DateTimeOffset CapturedAt);

public sealed record DiskStatus(string DriveName, long TotalBytes, long FreeBytes);

/// <summary>
/// Erweiterungspunkt für die tatsächliche Datenbeschaffung. Getrennt von
/// <see cref="SystemCenterModule"/>, damit dieses Projekt plattformneutral
/// bleibt - die echte Implementierung (WMI) liegt in
/// NWOS.SystemCenter.Windows.
/// </summary>
public interface ISystemStatusProvider
{
    Task<SystemStatusSnapshot> CaptureAsync(CancellationToken cancellationToken);
}
