namespace PitWall.Core.Models;

public enum ServiceStatus
{
    Healthy,
    Degraded,
    Failed,
    Recovering
}

public enum IncidentStatus
{
    Open,
    Investigating,
    Mitigated,
    Resolved
}

public enum DeploymentStatus
{
    Idle,
    Canary,
    Promoted,
    RolledBack
}

public enum EventType
{
    TelemetryUpdated,
    ServiceHealthChanged,
    IncidentCreated,
    IncidentResolved,
    DeploymentStarted,
    DeploymentPromoted,
    DeploymentRolledBack,
    AlertRaised,
    RaceSnapshotUpdated
}

public enum AlertSeverity
{
    Info,
    Warning,
    Critical
}
