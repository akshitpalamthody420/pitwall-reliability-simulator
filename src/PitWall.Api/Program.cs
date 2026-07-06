using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR;
using PitWall.Api.Hubs;
using PitWall.Api.Realtime;
using PitWall.Core.Models;
using PitWall.Core.Reporting;
using PitWall.Core.State;
using PitWall.Core.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddSignalR().AddJsonProtocol(options =>
{
    options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddSingleton(provider =>
{
    var env = provider.GetRequiredService<IWebHostEnvironment>();
    var dbPath = builder.Configuration.GetValue<string>("PitWall:DatabasePath")
        ?? Path.Combine(env.ContentRootPath, "data", "pitwall-events.db");

    return new SqliteEventStore(dbPath);
});

builder.Services.AddSingleton<PitWallStateMachine>();
builder.Services.AddSingleton<ReportGenerator>();
builder.Services.AddHostedService<TelemetryTicker>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/", (IWebHostEnvironment env) =>
{
    var indexPath = Path.Combine(env.WebRootPath ?? "wwwroot", "index.html");

    return File.Exists(indexPath)
        ? Results.File(indexPath, "text/html")
        : Results.NotFound("wwwroot/index.html was not found.");
});

app.MapHub<OperationsHub>("/opsHub");

app.MapGet("/api/snapshot", (PitWallStateMachine state) =>
{
    return Results.Ok(state.GetSnapshot());
});
app.MapGet("/api/strategy/recommendations",
    (PitWallStateMachine state) =>
{
    return Results.Ok(state.GetStrategyRecommendations());
});
app.MapGet("/api/services", (PitWallStateMachine state) =>
{
    return Results.Ok(state.GetServices());
});

app.MapGet("/api/incidents", (PitWallStateMachine state) =>
{
    return Results.Ok(state.GetIncidents());
});

app.MapGet("/api/deployments", (PitWallStateMachine state) =>
{
    return Results.Ok(state.GetDeployment());
});

app.MapGet("/api/events", async (SqliteEventStore store, int? limit, CancellationToken ct) =>
{
    var events = await store.GetEventsAsync(limit ?? 250, ct);
    return Results.Ok(events);
});
app.MapPost("/api/demo-scenarios/{scenario}",
    async (string scenario, PitWallStateMachine state, IHubContext<OperationsHub> hub, CancellationToken ct) =>
{
    try
    {
        var result = await state.ForceDemoScenarioAsync(scenario, ct);

        await BroadcastResultAsync(hub, result, ct);
        await hub.Clients.All.SendAsync("TelemetryUpdated", state.GetRaceCars(), ct);

        return Results.Ok(result);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});
app.MapPost("/api/failures/telemetry-delay",
    async (PitWallStateMachine state, IHubContext<OperationsHub> hub, CancellationToken ct) =>
{
    var result = await state.InjectTelemetryDelayAsync(ct);
    await BroadcastResultAsync(hub, result, ct);

    // Broadcast cars too, because car statuses can depend on service health.
    await hub.Clients.All.SendAsync("TelemetryUpdated", state.GetRaceCars(), ct);

    return Results.Ok(result);
});

app.MapPost("/api/failures/timing-packet-loss",
    async (PitWallStateMachine state, IHubContext<OperationsHub> hub, CancellationToken ct) =>
{
    var result = await state.InjectTimingPacketLossAsync(ct);
    await BroadcastResultAsync(hub, result, ct);

    await hub.Clients.All.SendAsync("TelemetryUpdated", state.GetRaceCars(), ct);

    return Results.Ok(result);
});

app.MapPost("/api/failures/strategy-engine-down",
    async (PitWallStateMachine state, IHubContext<OperationsHub> hub, CancellationToken ct) =>
{
    var result = await state.FailStrategyEngineAsync(ct);
    await BroadcastResultAsync(hub, result, ct);

    await hub.Clients.All.SendAsync("TelemetryUpdated", state.GetRaceCars(), ct);

    return Results.Ok(result);
});

app.MapPost("/api/recover",
    async (PitWallStateMachine state, IHubContext<OperationsHub> hub, CancellationToken ct) =>
{
    var result = await state.RecoverAllAsync(ct);
    await BroadcastResultAsync(hub, result, ct);

    await hub.Clients.All.SendAsync("TelemetryUpdated", state.GetRaceCars(), ct);

    return Results.Ok(result);
});

app.MapPost("/api/cars/{code}/pit",
    async (string code, PitWallStateMachine state, IHubContext<OperationsHub> hub, CancellationToken ct) =>
{
    try
    {
        var result = await state.PitCarAsync(code, ct);

        await BroadcastResultAsync(hub, result, ct);

        // Immediate car update so tyre age/status changes without waiting for next tick.
        await hub.Clients.All.SendAsync("TelemetryUpdated", state.GetRaceCars(), ct);

        return Results.Ok(result);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/deployments/canary",
    async (PitWallStateMachine state, IHubContext<OperationsHub> hub, CancellationToken ct) =>
{
    try
    {
        var result = await state.StartCanaryAsync(ct);
        await BroadcastResultAsync(hub, result, ct);
        return Results.Ok(result);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/deployments/promote",
    async (PitWallStateMachine state, IHubContext<OperationsHub> hub, CancellationToken ct) =>
{
    try
    {
        var result = await state.PromoteDeploymentAsync(ct);
        await BroadcastResultAsync(hub, result, ct);
        return Results.Ok(result);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/deployments/rollback",
    async (PitWallStateMachine state, IHubContext<OperationsHub> hub, CancellationToken ct) =>
{
    try
    {
        var result = await state.RollbackDeploymentAsync(ct);
        await BroadcastResultAsync(hub, result, ct);
        return Results.Ok(result);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/session/reset",
    async (PitWallStateMachine state, IHubContext<OperationsHub> hub, CancellationToken ct) =>
{
    var result = await state.ResetRaceAsync(ct);

    await BroadcastResultAsync(hub, result, ct);
    await hub.Clients.All.SendAsync("TelemetryUpdated", state.GetRaceCars(), ct);

    return Results.Ok(result);
});

app.MapGet("/api/report",
    async (
        SqliteEventStore store,
        PitWallStateMachine state,
        ReportGenerator reportGenerator,
        CancellationToken ct) =>
{
    var events = await store.GetEventsAsync(1000, ct);

    var markdown = reportGenerator.GenerateMarkdown(
        events,
        state.GetIncidents(),
        state.GetServices(),
        state.GetDeployment());

    return Results.Text(markdown, "text/markdown");
});

app.Run();

static async Task BroadcastResultAsync(
    IHubContext<OperationsHub> hub,
    OperationResult result,
    CancellationToken cancellationToken)
{
    await hub.Clients.All.SendAsync("ServicesUpdated", result.Services, cancellationToken);
    await hub.Clients.All.SendAsync("IncidentsUpdated", result.Incidents, cancellationToken);
    await hub.Clients.All.SendAsync("DeploymentUpdated", result.Deployment, cancellationToken);

    foreach (var evt in result.Events)
    {
        await hub.Clients.All.SendAsync(evt.EventType.ToString(), evt, cancellationToken);
        await hub.Clients.All.SendAsync("EventLogged", evt, cancellationToken);
    }

    foreach (var alert in result.Alerts)
    {
        await hub.Clients.All.SendAsync("AlertRaised", alert, cancellationToken);
    }
}