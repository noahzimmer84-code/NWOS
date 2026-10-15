namespace NWOS.Contracts;

/// <summary>
/// Einheitliches, versioniertes Nachrichtenformat für die Kommunikation über
/// Cardinal (Architekturdokument Abschnitt 7: "strukturierte, versionierte
/// Events"). Payload ist bewusst als einfacher String (z. B. JSON) gehalten,
/// damit NWOS.Contracts keine Abhängigkeit auf konkrete Fach-DTOs der Module
/// braucht - das hält die Modulgrenzen sauber.
/// </summary>
public sealed record ModuleMessage(
    string MessageId,
    string SourceModuleId,
    string TargetModuleId,
    string MessageType,
    string PayloadJson,
    PermissionScope RequiredScope,
    DateTimeOffset CreatedAt,
    int SchemaVersion = 1)
{
    public static ModuleMessage Create(
        string sourceModuleId,
        string targetModuleId,
        string messageType,
        string payloadJson,
        PermissionScope requiredScope) =>
        new(
            MessageId: Guid.NewGuid().ToString("N"),
            SourceModuleId: sourceModuleId,
            TargetModuleId: targetModuleId,
            MessageType: messageType,
            PayloadJson: payloadJson,
            RequiredScope: requiredScope,
            CreatedAt: DateTimeOffset.UtcNow);
}
