using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using NWOS.Contracts;
using Xunit;

namespace NWOS.Ipc.Tests;

[SupportedOSPlatform("windows")]
public class IpcIdentityTests
{
    [Fact]
    public async Task Server_overwrites_claimed_source_module_with_external_identity()
    {
        var pipeName = "nwos-ipc-test-" + Guid.NewGuid().ToString("N");
        var messenger = new RecordingMessenger();
        var server = new IpcServer(messenger, pipeName);
        using var cts = new CancellationTokenSource();
        var serverTask = server.RunAsync(cts.Token);

        // Client gibt sich fälschlich als Alice aus.
        var forged = ModuleMessage.Create("nwos.alice", "nwos.marc", "marc.closeApp", "{}", PermissionScope.None);
        await new IpcClient(pipeName: pipeName).SendAsync(forged, CancellationToken.None);

        Assert.Equal(IpcDefaults.ExternalCallerIdentity, messenger.LastReceived!.SourceModuleId);

        cts.Cancel();
        try { await serverTask; } catch (OperationCanceledException) { }
    }
}