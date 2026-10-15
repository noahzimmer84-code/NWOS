using Microsoft.Extensions.Logging.Abstractions;
using NWOS.Contracts;
using Xunit;

namespace NWOS.Cardinal.Tests;

public class PermissionManagerTests
{
    private static (ModuleRegistry registry, PermissionManager permissions) CreateSut()
    {
        var registry = new ModuleRegistry();
        var permissions = new PermissionManager(registry, NullLogger<PermissionManager>.Instance);
        return (registry, permissions);
    }

    [Fact]
    public void Denies_message_when_source_module_lacks_required_scope()
    {
        var (registry, permissions) = CreateSut();
        registry.Register(new FakeModule("nwos.marc"), PermissionScope.FileSystemRead);

        var message = ModuleMessage.Create(
            "nwos.marc", "nwos.alice", "test", "{}", PermissionScope.SystemChange);

        var decision = permissions.Evaluate(message);

        Assert.False(decision.Allowed);
    }

    [Fact]
    public void Always_denies_general_network_access_for_marc_even_if_granted()
    {
        var (registry, permissions) = CreateSut();
        // Bewusst "falsch" konfiguriert, um zu beweisen, dass die harte Regel
        // aus Abschnitt 9 nicht durch eine Fehlkonfiguration umgangen werden kann.
        registry.Register(new FakeModule("nwos.marc"), PermissionScope.NetworkGeneral);

        var message = ModuleMessage.Create(
            "nwos.marc", "nwos.cardinal", "browse", "{}", PermissionScope.NetworkGeneral);

        var decision = permissions.Evaluate(message);

        Assert.False(decision.Allowed);
    }

    [Fact]
    public void Allows_marc_research_scope_when_granted()
    {
        var (registry, permissions) = CreateSut();
        registry.Register(new FakeModule("nwos.marc"), PermissionScope.NetworkResearch);

        var message = ModuleMessage.Create(
            "nwos.marc", "nwos.cardinal", "research", "{}", PermissionScope.NetworkResearch);

        var decision = permissions.Evaluate(message);

        Assert.True(decision.Allowed);
        Assert.False(decision.RequiresUserConfirmation);
    }

    [Fact]
    public void Requires_user_confirmation_for_system_change_even_with_granted_scope()
    {
        var (registry, permissions) = CreateSut();
        registry.Register(new FakeModule("nwos.alice"), PermissionScope.SystemChange);

        var message = ModuleMessage.Create(
            "nwos.alice", "nwos.cardinal", "quarantine", "{}", PermissionScope.SystemChange);

        var decision = permissions.Evaluate(message);

        Assert.True(decision.Allowed);
        Assert.True(decision.RequiresUserConfirmation);
    }
}
