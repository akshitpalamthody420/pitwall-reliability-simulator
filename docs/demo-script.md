# Demo script

1. Open `http://localhost:5000`.
2. Point out that the track animation is driven by backend SignalR snapshots.
3. Click **Drop timing packets**. The timing feed degrades, an incident appears, the event log updates, and cars/timing rows show uncertainty.
4. Click **Fail strategy engine**. Alpine cars show strategy-blind status, a critical incident appears, and alerts are logged.
5. Click **Recover services**. Services return to healthy and incidents resolve.
6. Start a canary deployment, then rollback.
7. Generate the Markdown report and explain that it is generated from the SQLite event history.
