using System.Text;
using PitWall.Core.Models;

namespace PitWall.Core.Reporting;

public sealed class ReportGenerator
{
    public string GenerateMarkdown(
        IReadOnlyList<OperationsEvent> events,
        IReadOnlyList<Incident> incidents,
        IReadOnlyList<ServiceState> services,
        DeploymentState deployment,
        IReadOnlyList<RaceCarSnapshot> cars,
        IReadOnlyList<StrategyRecommendation> strategyRecommendations)
    {
        var orderedEvents = events
            .OrderBy(e => e.Timestamp)
            .ToList();

        var finalClassification = cars
            .OrderBy(c => c.Position)
            .ToList();

        var serviceFailures = orderedEvents
            .Where(IsServiceFailureEvent)
            .ToList();

        var pitStopEvents = orderedEvents
            .Where(IsPitStopEvent)
            .ToList();

        var deploymentEvents = orderedEvents
            .Where(IsDeploymentEvent)
            .ToList();

        var recoveryEvents = orderedEvents
            .Where(IsRecoveryEvent)
            .ToList();

        var sb = new StringBuilder();

        sb.AppendLine("# PitWall Reliability Report");
        sb.AppendLine();
        sb.AppendLine($"Generated: {DateTimeOffset.UtcNow:O}");
        sb.AppendLine();

        AppendRaceSummary(sb, finalClassification, incidents, serviceFailures, pitStopEvents, deploymentEvents);
        AppendFinalClassification(sb, finalClassification);
        AppendPitStops(sb, pitStopEvents);
        AppendStrategyRecommendations(sb, strategyRecommendations);
        AppendServiceFailures(sb, serviceFailures);
        AppendIncidents(sb, incidents);
        AppendDeploymentActions(sb, deployment, deploymentEvents);
        AppendRecoveryTimeline(sb, recoveryEvents);
        AppendServiceHealth(sb, services);
        AppendEventHistory(sb, orderedEvents);

        sb.AppendLine("## Interpretation");
        sb.AppendLine("This report is generated from backend state and the SQLite event log. It summarises the race state, strategy calls, service failures, incidents, recovery actions and deployment workflow observed during the session.");

        return sb.ToString();
    }

    private static void AppendRaceSummary(
        StringBuilder sb,
        IReadOnlyList<RaceCarSnapshot> cars,
        IReadOnlyList<Incident> incidents,
        IReadOnlyList<OperationsEvent> serviceFailures,
        IReadOnlyList<OperationsEvent> pitStopEvents,
        IReadOnlyList<OperationsEvent> deploymentEvents)
    {
        sb.AppendLine("## Race summary");

        if (cars.Count == 0)
        {
            sb.AppendLine("- Race state: no car snapshots available.");
        }
        else
        {
            var leader = cars.First();
            var alpineCars = cars
                .Where(c => c.Code.StartsWith("ALP", StringComparison.OrdinalIgnoreCase))
                .OrderBy(c => c.Position)
                .ToList();

            sb.AppendLine($"- Cars tracked: {cars.Count}");
            sb.AppendLine($"- Current leader: P{leader.Position} {leader.Code} — {leader.Driver}");
            sb.AppendLine($"- Pit stops recorded: {pitStopEvents.Count}");
            sb.AppendLine($"- Service failure events: {serviceFailures.Count}");
            sb.AppendLine($"- Incidents recorded: {incidents.Count}");
            sb.AppendLine($"- Deployment actions recorded: {deploymentEvents.Count}");

            if (alpineCars.Count > 0)
            {
                var alpineSummary = string.Join(
                    ", ",
                    alpineCars.Select(c => $"P{c.Position} {c.Code} ({c.Tyre}, age {c.TyreAge})"));

                sb.AppendLine($"- Alpine race state: {alpineSummary}");
            }
        }

        sb.AppendLine();
    }

    private static void AppendFinalClassification(
        StringBuilder sb,
        IReadOnlyList<RaceCarSnapshot> cars)
    {
        sb.AppendLine("## Final classification");

        if (cars.Count == 0)
        {
            sb.AppendLine("No classification available.");
            sb.AppendLine();
            return;
        }

        sb.AppendLine("| Pos | Car | Driver | Gap | Tyre | Age | Status |");
        sb.AppendLine("|---:|---|---|---:|---|---:|---|");

        foreach (var car in cars)
        {
            var gap = car.Position == 1
                ? "LEAD"
                : $"+{car.GapToLeaderSeconds:0.0}s";

            sb.AppendLine(
                $"| {car.Position} | {car.Code} | {car.Driver} | {gap} | {car.Tyre} | {car.TyreAge} | {car.Status} |");
        }

        sb.AppendLine();
    }

    private static void AppendPitStops(
        StringBuilder sb,
        IReadOnlyList<OperationsEvent> pitStopEvents)
    {
        sb.AppendLine("## Pit stops performed");

        if (pitStopEvents.Count == 0)
        {
            sb.AppendLine("No pit-stop events recorded.");
            sb.AppendLine();
            return;
        }

        foreach (var evt in pitStopEvents)
        {
            sb.AppendLine($"- {evt.Timestamp:HH:mm:ss} — {evt.Message}");
        }

        sb.AppendLine();
    }

    private static void AppendStrategyRecommendations(
        StringBuilder sb,
        IReadOnlyList<StrategyRecommendation> recommendations)
    {
        sb.AppendLine("## Strategy recommendations issued");

        if (recommendations.Count == 0)
        {
            sb.AppendLine("No strategy recommendations available.");
            sb.AppendLine();
            return;
        }

        foreach (var recommendation in recommendations.OrderBy(r => r.CarCode))
        {
            sb.AppendLine($"### {recommendation.CarCode} — {recommendation.Action}");
            sb.AppendLine($"- Driver: {recommendation.Driver}");
            sb.AppendLine($"- Urgency: {recommendation.Urgency}");
            sb.AppendLine($"- Current tyre: {recommendation.CurrentTyre}, age {recommendation.TyreAge}");
            sb.AppendLine($"- Recommended tyre: {recommendation.RecommendedTyre}");
            sb.AppendLine($"- Projected rejoin: P{recommendation.ProjectedRejoinPosition}");
            sb.AppendLine($"- Positions lost if pit: {recommendation.PositionsLostIfPit}");
            sb.AppendLine($"- Pit loss: {recommendation.PitLossSeconds:0}s");
            sb.AppendLine($"- Confidence: {recommendation.Confidence:P0}");
            sb.AppendLine($"- Reason: {recommendation.Reason}");

            if (recommendation.Factors.Count > 0)
            {
                sb.AppendLine("- Factors:");

                foreach (var factor in recommendation.Factors)
                {
                    sb.AppendLine($"  - {factor}");
                }
            }

            sb.AppendLine();
        }
    }

    private static void AppendServiceFailures(
        StringBuilder sb,
        IReadOnlyList<OperationsEvent> serviceFailures)
    {
        sb.AppendLine("## Service failures");

        if (serviceFailures.Count == 0)
        {
            sb.AppendLine("No service failure events recorded.");
            sb.AppendLine();
            return;
        }

        foreach (var evt in serviceFailures)
        {
            sb.AppendLine($"- {evt.Timestamp:HH:mm:ss} — **{evt.Service}** — {evt.Message}");
        }

        sb.AppendLine();
    }

    private static void AppendIncidents(
        StringBuilder sb,
        IReadOnlyList<Incident> incidents)
    {
        sb.AppendLine("## Incidents created/resolved");

        if (incidents.Count == 0)
        {
            sb.AppendLine("No incidents recorded.");
            sb.AppendLine();
            return;
        }

        foreach (var incident in incidents.OrderByDescending(i => i.CreatedAt))
        {
            sb.AppendLine($"- **{incident.Id}** {incident.Title} [{incident.Status}] — {incident.Impact}");
        }

        sb.AppendLine();
    }

    private static void AppendDeploymentActions(
        StringBuilder sb,
        DeploymentState deployment,
        IReadOnlyList<OperationsEvent> deploymentEvents)
    {
        sb.AppendLine("## Deployment actions");

        sb.AppendLine($"- Service: {deployment.Service}");
        sb.AppendLine($"- Stable version: {deployment.CurrentVersion}");
        sb.AppendLine($"- Candidate version: {deployment.CandidateVersion}");
        sb.AppendLine($"- Status: {deployment.Status}");
        sb.AppendLine($"- Canary: {deployment.CanaryPercent}%");
        sb.AppendLine($"- Message: {deployment.Message}");
        sb.AppendLine();

        if (deploymentEvents.Count == 0)
        {
            sb.AppendLine("No deployment events recorded.");
            sb.AppendLine();
            return;
        }

        foreach (var evt in deploymentEvents)
        {
            sb.AppendLine($"- {evt.Timestamp:HH:mm:ss} — {evt.Message}");
        }

        sb.AppendLine();
    }

    private static void AppendRecoveryTimeline(
        StringBuilder sb,
        IReadOnlyList<OperationsEvent> recoveryEvents)
    {
        sb.AppendLine("## Recovery timeline");

        if (recoveryEvents.Count == 0)
        {
            sb.AppendLine("No recovery events recorded.");
            sb.AppendLine();
            return;
        }

        foreach (var evt in recoveryEvents)
        {
            sb.AppendLine($"- {evt.Timestamp:HH:mm:ss} — {evt.Message}");
        }

        sb.AppendLine();
    }

    private static void AppendServiceHealth(
        StringBuilder sb,
        IReadOnlyList<ServiceState> services)
    {
        sb.AppendLine("## Current service health");

        foreach (var service in services.OrderBy(s => s.Name))
        {
            sb.AppendLine($"- **{service.Name}**: {service.Status} — {service.Message} ({service.LatencyMs} ms, {service.PacketLossPercent:0.0}% packet loss)");
        }

        sb.AppendLine();
    }

    private static void AppendEventHistory(
        StringBuilder sb,
        IReadOnlyList<OperationsEvent> events)
    {
        sb.AppendLine("## Event history");

        if (events.Count == 0)
        {
            sb.AppendLine("No events recorded.");
            sb.AppendLine();
            return;
        }

        foreach (var evt in events.TakeLast(75))
        {
            sb.AppendLine($"- {evt.Timestamp:HH:mm:ss} `{evt.EventType}` {evt.Message}");
        }

        sb.AppendLine();
    }

    private static bool IsPitStopEvent(OperationsEvent evt)
    {
        return Contains(evt.EventType.ToString(), "pit") ||
               Contains(evt.Message, "pit");
    }

    private static bool IsServiceFailureEvent(OperationsEvent evt)
    {
        return Contains(evt.EventType.ToString(), "failure") ||
               Contains(evt.EventType.ToString(), "degraded") ||
               Contains(evt.Message, "fail") ||
               Contains(evt.Message, "degraded") ||
               Contains(evt.Message, "packet loss") ||
               Contains(evt.Message, "delay");
    }

    private static bool IsDeploymentEvent(OperationsEvent evt)
    {
        return Contains(evt.EventType.ToString(), "deployment") ||
               Contains(evt.Message, "canary") ||
               Contains(evt.Message, "promote") ||
               Contains(evt.Message, "rollback") ||
               Contains(evt.Message, "deployment");
    }

    private static bool IsRecoveryEvent(OperationsEvent evt)
    {
        return Contains(evt.EventType.ToString(), "recover") ||
               Contains(evt.Message, "recover") ||
               Contains(evt.Message, "restored") ||
               Contains(evt.Message, "resolved");
    }

    private static bool Contains(string value, string search)
    {
        return value.Contains(search, StringComparison.OrdinalIgnoreCase);
    }
}