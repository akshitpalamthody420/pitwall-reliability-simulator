namespace PitWall.Core.Models;

public sealed record SystemSnapshot(
    IReadOnlyList<ServiceState> Services,
    IReadOnlyList<Incident> Incidents,
    DeploymentState Deployment,
    IReadOnlyList<RaceCarSnapshot> Cars,
    int Lap,
    string Mode);
