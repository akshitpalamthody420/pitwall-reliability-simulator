using Xunit;
using PitWall.Core.Models;
using PitWall.Core.State;
using PitWall.Core.Storage;

namespace PitWall.Core.Tests;

public sealed class StateMachineTests
{
    private static PitWallStateMachine CreateState(out SqliteEventStore store)
    {
        var path = Path.Combine(Path.GetTempPath(), $"pitwall-test-{Guid.NewGuid():N}.db");
        store = new SqliteEventStore(path);
        return new PitWallStateMachine(store);
    }

    [Fact]
    public async Task TelemetryDelay_CreatesIncidentAndDegradesService()
    {
        var state = CreateState(out _);

        var result = await state.InjectTelemetryDelayAsync();

        Assert.Contains(result.Services, s => s.Name == "Telemetry Ingest" && s.Status == ServiceStatus.Degraded);
        Assert.Contains(result.Incidents, i => i.Service == "Telemetry Ingest" && i.Status == IncidentStatus.Open);
    }

    [Fact]
    public async Task RecoverAll_ResolvesOpenIncidents()
    {
        var state = CreateState(out _);
        await state.InjectTimingPacketLossAsync();

        var result = await state.RecoverAllAsync();

        Assert.All(result.Services, service => Assert.Equal(ServiceStatus.Healthy, service.Status));
        Assert.All(result.Incidents, incident => Assert.Equal(IncidentStatus.Resolved, incident.Status));
    }

    [Fact]
    public async Task CanaryCanBePromotedAndRolledBack()
    {
        var state = CreateState(out _);

        var canary = await state.StartCanaryAsync();
        Assert.Equal(DeploymentStatus.Canary, canary.Deployment.Status);

        var promoted = await state.PromoteDeploymentAsync();
        Assert.Equal(DeploymentStatus.Promoted, promoted.Deployment.Status);

        var rolledBack = await state.RollbackDeploymentAsync();
        Assert.Equal(DeploymentStatus.RolledBack, rolledBack.Deployment.Status);
    }

    [Fact]
    public async Task RollbackWithoutDeployment_IsRejected()
    {
        var state = CreateState(out _);

        await Assert.ThrowsAsync<InvalidOperationException>(() => state.RollbackDeploymentAsync());
    }

    [Fact]
    public async Task EventStorePersistsEvents()
    {
        var state = CreateState(out var store);
        await state.FailStrategyEngineAsync();

        var events = await store.GetEventsAsync();

        Assert.Contains(events, e => e.EventType == EventType.ServiceHealthChanged && e.Service == "Strategy Engine");
        Assert.Contains(events, e => e.EventType == EventType.IncidentCreated);
    }
}
