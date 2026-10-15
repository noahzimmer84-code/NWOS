using System;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using NWOS.Contracts;
using Xunit;

namespace NWOS.Ipc.Tests;

[SupportedOSPlatform("windows")]
internal sealed class RecordingMessenger : IModuleMessenger
{
    public ModuleMessage? LastReceived { get; private set; }
    public ModuleMessageResult ResultToReturn { get; set; } =
        new(ModuleMessageOutcome.Delivered, null, null);

    public Task<ModuleMessageResult> SendAsync(ModuleMessage message, CancellationToken cancellationToken)
    {
        LastReceived = message;
        return Task.FromResult(ResultToReturn);
    }
}

[SupportedOSPlatform("windows")]
public class IpcServerClientTests
{
    // Jeder Test nutzt einen eigenen, zufälligen Pipe-Namen, damit parallel
    // laufende Tests sich nicht gegenseitig stören.
    private static string UniquePipeName() => "nwos-ipc-test-" + Guid.NewGuid().ToString("N");

    [Fact]
    public async Task Client_request_reaches_server_side_messenger_unchanged()
    {
        var pipeName = UniquePipeName();
        var messenger = new RecordingMessenger
        {
            ResultToReturn = new ModuleMessageResult(ModuleMessageOutcome.Delivered,
                ModuleMessage.Create("nwos.alice", "test", "reply", "{}", PermissionScope.None), null),
        };

        var server = new IpcServer(messenger, pipeName);
        using var serverCts = new CancellationTokenSource();
        var serverTask = server.RunAsync(serverCts.Token);

        var client = new IpcClient(pipeName: pipeName);
        var request = ModuleMessage.Create("test-caller", "nwos.alice", "alice.getRecentEvents", "{\"count\":5}", PermissionScope.None);

        var result = await client.SendAsync(request, CancellationToken.None);

        Assert.Equal(ModuleMessageOutcome.Delivered, result.Outcome);
        Assert.NotNull(messenger.LastReceived);
        Assert.Equal(request.MessageId, messenger.LastReceived!.MessageId);
        Assert.Equal(request.TargetModuleId, messenger.LastReceived.TargetModuleId);
        Assert.Equal(request.MessageType, messenger.LastReceived.MessageType);

        serverCts.Cancel();
        await SafeAwaitAsync(serverTask);
    }

    [Fact]
    public async Task Denied_outcome_and_reason_survive_the_round_trip()
    {
        var pipeName = UniquePipeName();
        var messenger = new RecordingMessenger
        {
            ResultToReturn = new ModuleMessageResult(ModuleMessageOutcome.Denied, null, "Scope nicht gewährt."),
        };

        var server = new IpcServer(messenger, pipeName);
        using var serverCts = new CancellationTokenSource();
        var serverTask = server.RunAsync(serverCts.Token);

        var client = new IpcClient(pipeName: pipeName);
        var request = ModuleMessage.Create("test-caller", "nwos.marc", "marc.launchApp", "{}", PermissionScope.ProcessControl);

        var result = await client.SendAsync(request, CancellationToken.None);

        Assert.Equal(ModuleMessageOutcome.Denied, result.Outcome);
        Assert.Equal("Scope nicht gewährt.", result.Reason);

        serverCts.Cancel();
        await SafeAwaitAsync(serverTask);
    }

    [Fact]
    public async Task Multiple_sequential_requests_are_each_handled_correctly()
    {
        var pipeName = UniquePipeName();
        var messenger = new RecordingMessenger();

        var server = new IpcServer(messenger, pipeName);
        using var serverCts = new CancellationTokenSource();
        var serverTask = server.RunAsync(serverCts.Token);

        var client = new IpcClient(pipeName: pipeName);

        for (var i = 0; i < 3; i++)
        {
            var request = ModuleMessage.Create("test-caller", "nwos.marc", $"marc.action{i}", "{}", PermissionScope.None);
            var result = await client.SendAsync(request, CancellationToken.None);

            Assert.Equal(ModuleMessageOutcome.Delivered, result.Outcome);
            Assert.Equal($"marc.action{i}", messenger.LastReceived!.MessageType);
        }

        serverCts.Cancel();
        await SafeAwaitAsync(serverTask);
    }

    [Fact]
    public async Task Client_returns_ModuleFailure_when_no_server_is_listening()
    {
        var client = new IpcClient(pipeName: UniquePipeName(), connectTimeout: TimeSpan.FromMilliseconds(300));
        var request = ModuleMessage.Create("test-caller", "nwos.marc", "marc.createFolder", "{}", PermissionScope.None);

        var result = await client.SendAsync(request, CancellationToken.None);

        Assert.Equal(ModuleMessageOutcome.ModuleFailure, result.Outcome);
        Assert.Contains("Cardinal", result.Reason);
    }

    private static async Task SafeAwaitAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
            // Erwarteter Abschluss nach Cancel().
        }
    }
}