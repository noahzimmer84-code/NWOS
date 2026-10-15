using NWOS.SystemCenter;

namespace NWOS.Host;

/// <summary>
/// Windows-spezifische Implementierungen, die ein plattformspezifisches
/// Bootstrap-Projekt (z. B. NWOS.Bootstrap.Windows) diesem plattformneutralen
/// Host optional mitgeben kann. Bleibt <c>null</c>, laufen die betroffenen
/// Module mit ihrem getesteten "nicht verfügbar"-Fallback statt mit Windows-
/// APIs, die dieses Projekt selbst nicht referenzieren darf (siehe
/// NWOS.Host.csproj-Begründung).
/// </summary>
public sealed record WindowsExtensions(
    ISystemStatusProvider? StatusProvider = null,
    IAutostartManager? AutostartManager = null);
