namespace PitWall.Core.Models;

public sealed record Incident(
    string Id,
    string Service,
    string Title,
    string Impact,
    IncidentStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ResolvedAt);
