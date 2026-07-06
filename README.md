# PitWall Reliability Simulator

A backend-driven C#/.NET race operations simulator that models live timing, pit-wall strategy calls, service failures, incident recovery, deployment rollback, SQLite event logging, and a real-time frontend.

The project is built to show practical software engineering for a motorsport-style reliability system: backend state machines, real-time updates, operational failure handling, and explainable strategy decisions.

## What the app does

PitWall Reliability Simulator runs a small live race simulation in the browser.

The frontend shows:

- An animated track view
- A live timing tower
- Service health
- Incidents
- Deployment state
- Strategy recommendations
- A generated reliability report

The important part is that the frontend is not faking the main behavior. Race state, service health, failures, incidents, deployment workflow, pit stops, and strategy recommendations are owned by the C# backend.

## Tech stack

- C# / .NET 10
- ASP.NET Core Minimal API
- SignalR for live updates
- SQLite for event logging
- xUnit for backend behavior tests
- HTML, CSS, and JavaScript canvas frontend

## Setup

Clone the repository:

```bash
git clone https://github.com/YOUR_USERNAME/pitwall-reliability-simulator.git
cd pitwall-reliability-simulator
```

Restore dependencies:

```bash
dotnet restore
```

Run tests:

```bash
dotnet test
```

Run the app:

```bash
dotnet run --project src/PitWall.Api
```

Open:

```text
http://localhost:5000
```

If port `5000` is already in use:

```bash
lsof -ti tcp:5000 | xargs kill -9
dotnet run --project src/PitWall.Api
```

## How to use the app

1. Open the app in the browser.
2. Click **Start race**.
3. Watch the timing tower and animated track update from backend telemetry.
4. Select a car and click **Pit selected car**.
5. Use the demo scenario buttons near the track view to force strategy situations.
6. Use the fault injection buttons to simulate service failures.
7. Use the deployment workflow to start canary, promote, or rollback.
8. Click **Generate report** to produce a markdown reliability report from backend state and event history.

## Main parts of the system

### Race simulation

The backend owns the race model.

It tracks:

- Car position
- Lap progress
- Tyre compound
- Tyre age
- Speed
- Gap to leader
- Pit stops
- Race status

The frontend canvas only visualizes snapshots received from the backend.

### Strategy recommendations

The Strategy Engine generates pit-wall style recommendations for Alpine cars.

It can produce calls such as:

- **UNDERCUT** — pit now to attack the car ahead with fresher tyres
- **OVERCUT** — stay out because the current tyre can still perform
- **COVER RIVAL** — pit now to stop the car behind from undercutting Alpine
- **EXTEND STINT** — tyres are poor, but pitting now would rejoin in traffic
- **PIT NOW** — tyre loss is high enough that stopping immediately is best
- **NO ADVICE** — the Strategy Engine service is unavailable

The recommendation panel shows:

- Action
- Urgency
- Current tyre
- Tyre age
- Pit loss
- Projected rejoin position
- Positions lost if pitting
- Confidence
- Reasoning factors

### Demo scenarios

The demo scenario buttons force specific backend race states so each strategy call can be shown during a short review.

Available scenarios:

- **Undercut**
- **Overcut**
- **Cover rival**
- **Bad pit window**
- **Tyre cliff**

These are labeled as demo scenarios because they intentionally force race states for demonstration and testing.

### Service reliability

The app models service health for operational systems such as:

- Telemetry ingest
- Timing feed parsing
- Track map streaming
- Incident storage
- Strategy recommendations

Fault injection buttons simulate problems such as telemetry delay, timing packet loss, and Strategy Engine failure.

### Incidents and recovery

When failures are injected, the backend creates incident records.

Recovering services updates the state machine and resolves operational problems.

### Deployment workflow

The app includes a deployment safety workflow for the Strategy Engine.

Supported actions:

- **Start canary**
- **Promote**
- **Rollback**

The backend validates the workflow so invalid deployment transitions are rejected.

### Event logging

Operational events are persisted in SQLite.

The event log records:

- Failures
- Recoveries
- Pit stops
- Deployment actions
- Race updates
- Report-relevant activity

### Reliability report

The report generator creates a markdown report from backend state and the event log.

The report includes:

- Race summary
- Final classification
- Pit stops performed
- Strategy recommendations issued
- Service failures
- Incidents created/resolved
- Deployment actions
- Recovery timeline
- Current service health
- Event history

## Testing

Run all tests:

```bash
dotnet test
```

The backend tests cover:

- Race reset behavior
- Pit stop behavior
- Strategy Engine failure behavior
- Forced strategy demo scenarios
- Deployment workflow validation
- Rollback rules
- Service recovery behavior

## Demo script

A short demo flow:

1. Start the race.
2. Show that timing and animation update from backend state.
3. Click **Undercut** and show the recommendation.
4. Click **Overcut** and show the recommendation.
5. Click **Cover rival** and show the recommendation.
6. Click **Bad pit window** and show `EXTEND STINT`.
7. Click **Tyre cliff** and show `PIT NOW`.
8. Pit an Alpine car and show tyre/gap/status changes.
9. Fail the Strategy Engine and show `NO ADVICE`.
10. Recover services and show recommendations return.
11. Start canary deployment.
12. Promote or rollback.
13. Generate the reliability report.

## Notes

This is a deterministic simulator, not a full physics model.

The race logic is designed to make operational behavior, strategy decisions, and failure recovery easy to demonstrate and test.

The goal is to show backend engineering, state management, reliability thinking, and real-time system behavior in a motorsport-style setting.