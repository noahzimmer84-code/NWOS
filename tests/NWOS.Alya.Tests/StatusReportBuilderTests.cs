using System.Text.Json;
using NWOS.Contracts;
using Xunit;

namespace NWOS.Alya.Tests;

public class StatusReportBuilderTests
{
    [Fact]
    public async Task Without_messenger_reports_incomplete_status_instead_of_throwing()
    {
        var builder = new StatusReportBuilder(messenger: null, selfModuleId: "nwos.alya");

        var report = await builder.BuildAsync(CancellationToken.None);

        Assert.Contains(report.Entries, e => e.Text.Contains("Keine Verbindung zu Cardinal"));
    }

    [Fact]
    public async Task When_alice_denies_the_request_status_report_explains_why_instead_of_crashing()
    {
        var messenger = new FakeModuleMessenger { OutcomeToReturn = ModuleMessageOutcome.Denied };
        var builder = new StatusReportBuilder(messenger, "nwos.alya");

        var report = await builder.BuildAsync(CancellationToken.None);

        Assert.Contains(report.Entries, e => e.Category == "Sicherheit" && e.Text.Contains("Denied"));
    }

    [Fact]
    public async Task When_alice_returns_events_they_appear_as_entries()
    {
        var eventsJson = JsonSerializer.Serialize(new[]
        {
            new { ruleId = "double-extension-executable", explanation = "Testfund" },
        });

        var response = ModuleMessage.Create("nwos.alice", "nwos.alya", "alice.getRecentEvents.result", eventsJson, PermissionScope.None);
        var messenger = new FakeModuleMessenger { OutcomeToReturn = ModuleMessageOutcome.Delivered, ResponseToReturn = response };

        var builder = new StatusReportBuilder(messenger, "nwos.alya");
        var report = await builder.BuildAsync(CancellationToken.None);

        Assert.Contains(report.Entries, e => e.Text.Contains("double-extension-executable") && e.Text.Contains("Testfund"));
    }

    [Fact]
    public async Task Request_to_alice_uses_none_scope_since_it_is_a_read_only_query()
    {
        var messenger = new FakeModuleMessenger { OutcomeToReturn = ModuleMessageOutcome.Denied };
        var builder = new StatusReportBuilder(messenger, "nwos.alya");

        await builder.BuildAsync(CancellationToken.None);

        Assert.Equal(PermissionScope.None, messenger.LastSentMessage!.RequiredScope);
        Assert.Equal("nwos.alice", messenger.LastSentMessage.TargetModuleId);
        Assert.Equal("nwos.alya", messenger.LastSentMessage.SourceModuleId);
    }

    [Fact]
    public async Task No_events_results_in_a_reassuring_default_entry()
    {
        var response = ModuleMessage.Create("nwos.alice", "nwos.alya", "alice.getRecentEvents.result", "[]", PermissionScope.None);
        var messenger = new FakeModuleMessenger { OutcomeToReturn = ModuleMessageOutcome.Delivered, ResponseToReturn = response };

        var builder = new StatusReportBuilder(messenger, "nwos.alya");
        var report = await builder.BuildAsync(CancellationToken.None);

        Assert.Contains(report.Entries, e => e.Text.Contains("Keine sicherheitsrelevanten Ereignisse"));
    }
}
