using Microsoft.Extensions.Logging.Abstractions;
using NWOS.Contracts;
using Xunit;

namespace NWOS.Cardinal.Tests;

public class MessageScopePolicyTests
{
    private static PermissionManager CreateWithExternalUi()
    {
        var registry = new ModuleRegistry();
        registry.GrantExternalScope("external.ipc", PermissionScope.FileSystemRead | PermissionScope.FileSystemWrite);
        return new PermissionManager(registry, NullLogger<PermissionManager>.Instance);
    }

    [Fact]
    public void Sender_cannot_bypass_check_by_declaring_scope_none()
    {
        var permissions = CreateWithExternalUi();

        // Der Absender behauptet "None", die Policy verlangt für alice.quarantine SystemChange.
        var message = ModuleMessage.Create("external.ipc", "nwos.alice", "alice.quarantine", "{}", PermissionScope.None);

        Assert.False(permissions.Evaluate(message).Allowed);
    }

    [Fact]
    public void External_ui_cannot_control_processes_even_if_it_declares_less()
    {
        var permissions = CreateWithExternalUi();
        var message = ModuleMessage.Create("external.ipc", "nwos.marc", "marc.launchApp", "{}", PermissionScope.None);

        Assert.False(permissions.Evaluate(message).Allowed);
    }

    [Fact]
    public void External_ui_may_write_box_and_widget_state()
    {
        var permissions = CreateWithExternalUi();

        Assert.True(permissions.Evaluate(
            ModuleMessage.Create("external.ipc", "nwos.desktopshell", "box.create", "{}", PermissionScope.None)).Allowed);
        Assert.True(permissions.Evaluate(
            ModuleMessage.Create("external.ipc", "nwos.widgets", "widget.create", "{}", PermissionScope.None)).Allowed);
    }

    [Theory]
    [InlineData("box.create", PermissionScope.FileSystemWrite)]
    [InlineData("marc.closeApp", PermissionScope.ProcessControl)]
    [InlineData("alice.restore", PermissionScope.SystemChange)]
    [InlineData("alya.greet", PermissionScope.None)]
    [InlineData("unbekannt.typ", PermissionScope.None)]
    public void Policy_returns_expected_minimum(string messageType, PermissionScope expected)
    {
        Assert.Equal(expected, MessageScopePolicy.MinimumScopeFor(messageType));
    }
}
