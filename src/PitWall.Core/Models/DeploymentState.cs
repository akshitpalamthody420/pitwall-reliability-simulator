namespace PitWall.Core.Models;

public sealed record DeploymentState(
    string Service,
    string CurrentVersion,
    string CandidateVersion,
    DeploymentStatus Status,
    int CanaryPercent,
    string Message,
    DateTimeOffset UpdatedAt);
