using Xunit;
using PitWall.Core.Models;
using PitWall.Core.State;
using PitWall.Core.Storage;

namespace PitWall.Core.Tests;

public sealed class PitWallStateMachineBehaviorTests
{
    private static PitWallStateMachine CreateStateMachine()
    {
        var dbPath = Path.Combine(
            Path.GetTempPath(),
            $"pitwall-test-{Guid.NewGuid():N}.db");

        var store = new SqliteEventStore(dbPath);

        return new PitWallStateMachine(store);
    }

    [Fact]
    public void NewStateMachine_StartsWithHealthyServicesAndRaceCars()
    {
        var state = CreateStateMachine();

        var services = state.GetServices();
        var cars = state.GetRaceCars();

        Assert.NotEmpty(services);
        Assert.All(services, service =>
            Assert.Equal(ServiceStatus.Healthy, service.Status));

        Assert.Equal(8, cars.Count);
        Assert.Contains(cars, car => car.Code == "ALP1");
        Assert.Contains(cars, car => car.Code == "ALP2");
        Assert.All(cars, car =>
        {
            Assert.True(car.Position >= 1);
            Assert.True(car.Lap >= 1);
            Assert.True(car.GapToLeaderSeconds >= 0);
        });
    }

    [Fact]
    public async Task RaceModel_AdvancesCarsAndCreatesGaps()
    {
        var state = CreateStateMachine();

        _ = state.GetRaceCars();

        await Task.Delay(1200);

        var cars = state.GetRaceCars();

        Assert.Equal(8, cars.Count);
        Assert.Contains(cars, car => car.SpeedKph > 0);
        Assert.Contains(cars, car => car.GapToLeaderSeconds > 0);
        Assert.Equal(1, cars.Min(car => car.Position));
        Assert.Equal(8, cars.Max(car => car.Position));
    }

    [Fact]
    public async Task PitCar_ChangesTyreAndMarksCarAsPitting()
    {
        var state = CreateStateMachine();

        var before = state.GetRaceCars()
            .Single(car => car.Code == "ALP1");

        var result = await state.PitCarAsync("ALP1");

        var after = state.GetRaceCars()
            .Single(car => car.Code == "ALP1");

        Assert.NotEqual(before.Tyre, after.Tyre);
        Assert.Equal("Hard", after.Tyre);
        Assert.Equal("Pit stop", after.Status);

        Assert.Contains(result.Alerts, alert =>
            alert.Title == "Pit stop" &&
            alert.Message.Contains("ALP1", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PitCar_RejectsUnknownCarCode()
    {
        var state = CreateStateMachine();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => state.PitCarAsync("XXX"));

        Assert.Contains("Unknown car code", ex.Message);
    }

    [Fact]
    public async Task StrategyEngineFailure_ReturnsNoAdvice()
    {
        var state = CreateStateMachine();

        await state.FailStrategyEngineAsync();

        var recommendations = state.GetStrategyRecommendations();

        Assert.Equal(2, recommendations.Count);
        Assert.All(recommendations, recommendation =>
        {
            Assert.Equal("NO ADVICE", recommendation.Action);
            Assert.Equal("Unavailable", recommendation.Urgency);
            Assert.Equal("-", recommendation.RecommendedTyre);
            Assert.Contains("Strategy Engine is unavailable", recommendation.Reason);
        });

        var strategyService = state.GetServices()
            .Single(service => service.Name == "Strategy Engine");

        Assert.Equal(ServiceStatus.Failed, strategyService.Status);
    }

    [Fact]
    public async Task RecoverAll_RestoresFailedServicesAndRecommendations()
    {
        var state = CreateStateMachine();

        await state.FailStrategyEngineAsync();
        await state.RecoverAllAsync();

        var services = state.GetServices();
        var recommendations = state.GetStrategyRecommendations();

        Assert.All(services, service =>
            Assert.Equal(ServiceStatus.Healthy, service.Status));

        Assert.All(recommendations, recommendation =>
            Assert.NotEqual("NO ADVICE", recommendation.Action));
    }

    [Fact]
    public async Task ResetRace_ClearsIncidentsAndRestoresCleanRaceState()
    {
        var state = CreateStateMachine();

        await state.InjectTelemetryDelayAsync();
        await state.PitCarAsync("ALP1");

        Assert.NotEmpty(state.GetIncidents());

        await state.ResetRaceAsync();

        var incidents = state.GetIncidents();
        var services = state.GetServices();
        var cars = state.GetRaceCars();

        Assert.Empty(incidents);

        Assert.All(services, service =>
            Assert.Equal(ServiceStatus.Healthy, service.Status));

        Assert.All(cars, car =>
        {
            Assert.Equal(1, car.Lap);
            Assert.Equal(0, car.TyreAge);
            Assert.NotEqual("Pit stop", car.Status);
            Assert.True(car.GapToLeaderSeconds >= 0);
        });
    }

    [Fact]
    public async Task Deployment_CannotPromoteWithoutCanary()
    {
        var state = CreateStateMachine();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => state.PromoteDeploymentAsync());

        Assert.Contains("no canary deployment is active", ex.Message);
    }

    [Fact]
    public async Task Deployment_CanaryPromoteRollbackWorkflowIsValidated()
    {
        var state = CreateStateMachine();

        await state.StartCanaryAsync();

        var canary = state.GetDeployment();

        Assert.Equal(DeploymentStatus.Canary, canary.Status);
        Assert.Equal(20, canary.CanaryPercent);

        await state.PromoteDeploymentAsync();

        var promoted = state.GetDeployment();

        Assert.Equal(DeploymentStatus.Promoted, promoted.Status);
        Assert.Equal(100, promoted.CanaryPercent);
        Assert.Equal(promoted.CurrentVersion, promoted.CandidateVersion);

        await state.RollbackDeploymentAsync();

        var rolledBack = state.GetDeployment();

        Assert.Equal(DeploymentStatus.RolledBack, rolledBack.Status);
        Assert.Equal(0, rolledBack.CanaryPercent);
    }

    [Fact]
    public async Task TelemetryDelay_CreatesIncidentAndDegradesTelemetryService()
    {
        var state = CreateStateMachine();

        var result = await state.InjectTelemetryDelayAsync();

        var telemetry = state.GetServices()
            .Single(service => service.Name == "Telemetry Ingest");

        var incidents = state.GetIncidents();

        Assert.Equal(ServiceStatus.Degraded, telemetry.Status);
        Assert.NotEmpty(incidents);

        Assert.Contains(result.Alerts, alert =>
            alert.Title == "Telemetry delay");
    }
}