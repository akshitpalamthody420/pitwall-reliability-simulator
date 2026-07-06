namespace PitWall.Core.Models;

public sealed record ServiceState(
    string Name,
    ServiceStatus Status,
    int LatencyMs,
    double PacketLossPercent,
    string Message,
    DateTimeOffset UpdatedAt);
