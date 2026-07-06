namespace PitWall.Core.Models;

public sealed record OperationResult(
    IReadOnlyList<ServiceState> Services,
    IReadOnlyList<Incident> Incidents,
    DeploymentState Deployment,
    IReadOnlyList<OperationsEvent> Events,
    IReadOnlyList<Alert> Alerts);
