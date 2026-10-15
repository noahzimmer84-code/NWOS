using System.Text.Json;

namespace NWOS.Contracts;

/// <summary>
/// Gemeinsame JSON-Einstellungen für Modul-Antwort-Payloads. Alle
/// MessageType-Verträge in diesem Projekt sind camelCase dokumentiert (siehe
/// die XML-Doc-Kommentare in den einzelnen *Module-Klassen, z. B.
/// "ruleId", "filesMoved", "isEnabled") - jedes Modul MUSS diese Options beim
/// Serialisieren seiner Antwort-Payloads verwenden, sonst weichen
/// tatsächliche Feldnamen (PascalCase, .NET-Standard) vom dokumentierten
/// Vertrag ab. Gilt nur für die nach außen gehende Modul-Kommunikation - rein
/// interne Persistenz (z. B. lokale JSON-Zustandsdateien) ist davon
/// unabhängig und braucht diese Options nicht.
/// </summary>
public static class NwosJson
{
    public static readonly JsonSerializerOptions ResponseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
}
