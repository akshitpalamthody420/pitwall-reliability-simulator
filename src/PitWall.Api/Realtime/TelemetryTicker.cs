using Microsoft.AspNetCore.SignalR;
using PitWall.Api.Hubs;
using PitWall.Core.Models;
using PitWall.Core.State;

namespace PitWall.Api.Realtime;

public sealed class TelemetryTicker : BackgroundService
{
    private readonly PitWallStateMachine _stateMachine;
    private readonly IHubContext<OperationsHub> _hub;
    private readonly ILogger<TelemetryTicker> _logger;

    public TelemetryTicker(PitWallStateMachine stateMachine, IHubContext<OperationsHub> hub, ILogger<TelemetryTicker> logger)
    {
        _stateMachine = stateMachine;
        _hub = hub;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(220));
        var tick = 0;
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var cars = _stateMachine.GetRaceCars();
                await _hub.Clients.All.SendAsync("TelemetryUpdated", cars, stoppingToken);

                if (++tick % 20 == 0)
                {
                    var evt = await _stateMachine.LogRaceSnapshotAsync(stoppingToken);
                    await _hub.Clients.All.SendAsync("EventLogged", evt, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to broadcast telemetry tick");
            }
        }
    }
}
