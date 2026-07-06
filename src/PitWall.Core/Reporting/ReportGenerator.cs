using System.Text;
using PitWall.Core.Models;

namespace PitWall.Core.Reporting;

public sealed class ReportGenerator
{
    public string GenerateMarkdown(
        IReadOnlyList<OperationsEvent> events,
        IReadOnlyList<Incident> incidents,
        IReadOnlyList<ServiceState> services,
        DeploymentState deployment)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# PitWall Reliability Report");
        sb.AppendLine();
        sb.AppendLine($"Generated: {DateTimeOffset.UtcNow:O}");
        sb.AppendLine();
        sb.AppendLine("## Service health");
        foreach (var service in services.OrderBy(s => s.Name))
        {
            sb.AppendLine($"- **{service.Name}**: {service.Status} — {service.Message} ({service.LatencyMs} ms, {service.PacketLossPercent:0.0}% packet loss)");
        }
        sb.AppendLine();
        sb.AppendLine("## Deployment state");
        sb.AppendLine($"- Service: {deployment.Service}");
        sb.AppendLine($"- Stable version: {deployment.CurrentVersion}");
        sb.AppendLine($"- Candidate version: {deployment.CandidateVersion}");
        sb.AppendLine($"- Status: {deployment.Status}");
        sb.AppendLine($"- Canary: {deployment.CanaryPercent}%");
        sb.AppendLine($"- Message: {deployment.Message}");
        sb.AppendLine();
        sb.AppendLine("## Incidents");
        if (incidents.Count == 0)
        {
            sb.AppendLine("No incidents recorded.");
        }
        else
        {
            foreach (var incident in incidents.OrderByDescending(i => i.CreatedAt))
            {
                sb.AppendLine($"- **{incident.Id}** {incident.Title} [{incident.Status}] — {incident.Impact}");
            }
        }
        sb.AppendLine();
        sb.AppendLine("## Event history");
        foreach (var evt in events.TakeLast(50))
        {
            sb.AppendLine($"- {evt.Timestamp:HH:mm:ss} `{evt.EventType}` {evt.Message}");
        }
        sb.AppendLine();
        sb.AppendLine("## Interpretation");
        sb.AppendLine("This report is generated from the SQLite event log, not from hardcoded frontend text. It summarises service state, incidents, deployment state and recent operational events.");
        return sb.ToString();
    }
}
