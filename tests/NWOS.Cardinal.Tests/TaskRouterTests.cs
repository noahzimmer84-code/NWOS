using Microsoft.Extensions.Logging.Abstractions;
using NWOS.Contracts;
using Xunit;

namespace NWOS.Cardinal.Tests;

public class TaskRouterTests
{
    private static (ModuleRegistry registry, TaskRouter router) CreateSut()
    {
        var registry = new ModuleRegistry();
        var permissions = new PermissionManager(registry, NullLogger<PermissionManager>.Instance);
        var router = new TaskRouter(registry, permissions, NullLogger<TaskRouter>.Instance);
        return (registry, router);
    }

    [Fact]
    public async Task Delivers_message_to_registered_target_module()
    {
        var (registry, router) = CreateSut();
        registry.Register(new FakeModule("nwos.marc"), PermissionScope.FileSystemRead);
        registry.Register(new FakeModule("nwos.alice"), PermissionScope.FileSystemRead);

        var message = ModuleMessage.Create(
            "nwos.alice", "nwos.marc", "ping", "{}", PermissionScope.FileSystemRead);

        var result = await router.RouteAsync(message, CancellationToken.None);

        Assert.Equal(RoutingOutcome.Delivered, result.Outcome);
        Assert.NotNull(result.Response);
    }

    [Fact]
    public async Task Returns_denied_when_target_module_not_registered()
    {
        var (registry, router) = CreateSut();
        registry.Register(new FakeModule("nwos.alice"), PermissionScope.FileSystemRead);

        var message = ModuleMessage.Create(
            "nwos.alice", "nwos.unknown", "ping", "{}", PermissionScope.FileSystemRead);

        var result = await router.RouteAsync(message, CancellationToken.None);

        Assert.Equal(RoutingOutcome.Denied, result.Outcome);
    }

    [Fact]
    public async Task Isolates_target_module_exception_as_ModuleFailure_instead_of_throwing()
    {
        var (registry, router) = CreateSut();
        var faultyModule = new FakeModule("nwos.marc") { ThrowOnHandleMessage = true };
        registry.Register(new FakeModule("nwos.alice"), PermissionScope.FileSystemRead);
        registry.Register(faultyModule, PermissionScope.FileSystemRead);

        var message = ModuleMessage.Create(
            "nwos.alice", "nwos.marc", "ping", "{}", PermissionScope.FileSystemRead);

        var result = await router.RouteAsync(message, CancellationToken.None);

        Assert.Equal(RoutingOutcome.ModuleFailure, result.Outcome);
    }

    [Fact]
    public async Task Does_not_auto_deliver_system_change_messages_without_confirmation()
    {
        var (registry, router) = CreateSut();
        registry.Register(new FakeModule("nwos.alice"), PermissionScope.SystemChange);
        registry.Register(new FakeModule("nwos.cardinal"), PermissionScope.None);

        var message = ModuleMessage.Create(
            "nwos.alice", "nwos.cardinal", "quarantine-file", "{}", PermissionScope.SystemChange);

        var result = await router.RouteAsync(message, CancellationToken.None);

        Assert.Equal(RoutingOutcome.PendingUserConfirmation, result.Outcome);
    }
}
