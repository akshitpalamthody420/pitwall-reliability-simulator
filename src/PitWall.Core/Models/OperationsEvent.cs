namespace PitWall.Core.Models;

public sealed record OperationsEvent(
    long Id,
    DateTimeOffset Timestamp,
    EventType EventType,
    string? Service,
    string? OldState,
    string? NewState,
    string Message);
