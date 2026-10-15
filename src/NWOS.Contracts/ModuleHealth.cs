using System;

namespace NWOS.Contracts;

public enum ModuleStatus
{
    Healthy,
    Degraded,
    Unresponsive,
    Disabled
}

public sealed record ModuleHealth
{
    public string ModuleId { get; init; } = string.Empty;
    public ModuleStatus Status { get; init; } = ModuleStatus.Healthy;
    public bool IsHealthy => Status == ModuleStatus.Healthy;
    public string StatusMessage { get; init; } = string.Empty;
    public DateTimeOffset? Timestamp { get; init; }

    public ModuleHealth() { }

    public ModuleHealth(string moduleId, ModuleStatus status, string statusMessage = "", DateTimeOffset? timestamp = null)
    {
        ModuleId = moduleId;
        Status = status;
        StatusMessage = statusMessage;
        Timestamp = timestamp ?? DateTimeOffset.UtcNow;
    }

    public ModuleHealth(string moduleId, bool isHealthy, string statusMessage = "", DateTimeOffset? timestamp = null)
    {
        ModuleId = moduleId;
        Status = isHealthy ? ModuleStatus.Healthy : ModuleStatus.Degraded;
        StatusMessage = statusMessage;
        Timestamp = timestamp ?? DateTimeOffset.UtcNow;
    }
}