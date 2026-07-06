namespace PitWall.Core.Models;

public sealed record Alert(
    string Id,
    DateTimeOffset Timestamp,
    AlertSeverity Severity,
    string Title,
    string Message,
    string RecommendedAction);
