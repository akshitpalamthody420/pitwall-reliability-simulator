# PitWall Reliability Simulator

PitWall Reliability Simulator is a C#/.NET software-engineering project that keeps the visual style of a trackside motorsport control room, but the important behaviour is backend-driven.

It is not a real F1 telemetry system and it does not use proprietary race data. It is a reliability simulator for live engineering operations.

## What it demonstrates

- ASP.NET Core backend
- SignalR live event streaming
- C# service-health state machines
- Incident creation and resolution workflow
- Deployment canary / promote / rollback workflow
- SQLite event logging
- Backend-generated race-car snapshots
- Animated browser track map
- Markdown report generation from stored events
- xUnit tests
- GitHub Actions CI

## User flow

The reviewer opens the app and sees a live trackside view with moving cars. They can inject failures such as telemetry delay, timing packet loss, or strategy-engine failure. The backend changes service state, creates incidents, persists events in SQLite, broadcasts updates over SignalR, and the frontend updates the track view, timing tower, service panel, incident list and event log.

## Run locally

```bash
dotnet restore
dotnet test
dotnet run --project src/PitWall.Api
```

Open:

```text
http://localhost:5000
```

If port 5000 is busy:

```bash
dotnet run --project src/PitWall.Api --urls http://localhost:5050
```

Then open:

```text
http://localhost:5050
```

## Main endpoints

```text
GET  /api/snapshot
GET  /api/services
GET  /api/incidents
GET  /api/events
GET  /api/deployments
POST /api/failures/telemetry-delay
POST /api/failures/timing-packet-loss
POST /api/failures/strategy-engine-down
POST /api/recover
POST /api/deployments/canary
POST /api/deployments/promote
POST /api/deployments/rollback
GET  /api/report
```

SignalR hub:

```text
/opsHub
```

Events broadcast to the frontend include:

```text
TelemetryUpdated
ServicesUpdated
IncidentsUpdated
DeploymentUpdated
EventLogged
AlertRaised
```

## Why this is not hardcoded dashboard theatre

The frontend does not create incidents or service failures by itself. The flow is:

```text
User clicks failure button
↓
Frontend calls ASP.NET Core endpoint
↓
C# state machine changes service state
↓
Incident is created
↓
Event is saved to SQLite
↓
SignalR broadcasts updates
↓
Frontend redraws cars/panels from backend state
↓
Report is generated from stored event history
```

## Limitations

- The car data is simulated by the backend, not real F1 telemetry.
- The app is a reliability simulator, not a race-strategy model.
- The track map is a simplified visual layer, not a real circuit.
- The SignalR browser client is loaded from a CDN.

## CV bullet

Built PitWall Reliability Simulator, a C#/.NET reliability simulator for live engineering operations using ASP.NET Core, SignalR event streaming, service-health state machines, incident workflows, canary deployment and rollback simulation, SQLite event logging, animated backend-driven track visualisation, automated tests and Markdown report generation.
