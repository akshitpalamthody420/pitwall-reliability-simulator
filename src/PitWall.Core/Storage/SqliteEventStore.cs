using Microsoft.Data.Sqlite;
using PitWall.Core.Models;

namespace PitWall.Core.Storage;

public sealed class SqliteEventStore
{
    private readonly string _connectionString;

    public SqliteEventStore(string databasePath)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString();
        Initialise();
    }

    public async Task<OperationsEvent> AppendEventAsync(EventType type, string? service, string? oldState, string? newState, string message, CancellationToken ct = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        var timestamp = DateTimeOffset.UtcNow;
        var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO events(timestamp, event_type, service, old_state, new_state, message)
VALUES ($timestamp, $eventType, $service, $oldState, $newState, $message);
SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("$timestamp", timestamp.ToString("O"));
        command.Parameters.AddWithValue("$eventType", type.ToString());
        command.Parameters.AddWithValue("$service", (object?)service ?? DBNull.Value);
        command.Parameters.AddWithValue("$oldState", (object?)oldState ?? DBNull.Value);
        command.Parameters.AddWithValue("$newState", (object?)newState ?? DBNull.Value);
        command.Parameters.AddWithValue("$message", message);

        var id = (long)(await command.ExecuteScalarAsync(ct) ?? 0L);
        return new OperationsEvent(id, timestamp, type, service, oldState, newState, message);
    }

    public async Task SaveIncidentAsync(Incident incident, CancellationToken ct = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO incidents(id, service, title, impact, status, created_at, resolved_at)
VALUES ($id, $service, $title, $impact, $status, $createdAt, $resolvedAt)
ON CONFLICT(id) DO UPDATE SET
    status = excluded.status,
    resolved_at = excluded.resolved_at;";
        command.Parameters.AddWithValue("$id", incident.Id);
        command.Parameters.AddWithValue("$service", incident.Service);
        command.Parameters.AddWithValue("$title", incident.Title);
        command.Parameters.AddWithValue("$impact", incident.Impact);
        command.Parameters.AddWithValue("$status", incident.Status.ToString());
        command.Parameters.AddWithValue("$createdAt", incident.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$resolvedAt", incident.ResolvedAt?.ToString("O") ?? (object)DBNull.Value);

        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<OperationsEvent>> GetEventsAsync(int limit = 250, CancellationToken ct = default)
    {
        var events = new List<OperationsEvent>();
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        var command = connection.CreateCommand();
        command.CommandText = @"
SELECT id, timestamp, event_type, service, old_state, new_state, message
FROM events
ORDER BY id DESC
LIMIT $limit;";
        command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 5000));

        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            events.Add(ReadEvent(reader));
        }

        events.Reverse();
        return events;
    }

    public async Task<IReadOnlyList<Incident>> GetIncidentsAsync(CancellationToken ct = default)
    {
        var incidents = new List<Incident>();
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        var command = connection.CreateCommand();
        command.CommandText = @"
SELECT id, service, title, impact, status, created_at, resolved_at
FROM incidents
ORDER BY created_at DESC;";

        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            incidents.Add(new Incident(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                Enum.Parse<IncidentStatus>(reader.GetString(4)),
                DateTimeOffset.Parse(reader.GetString(5)),
                reader.IsDBNull(6) ? null : DateTimeOffset.Parse(reader.GetString(6))));
        }

        return incidents;
    }

    public async Task ClearAsync(CancellationToken ct = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);
        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM events; DELETE FROM incidents;";
        await command.ExecuteNonQueryAsync(ct);
    }

    private void Initialise()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
CREATE TABLE IF NOT EXISTS events (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    timestamp TEXT NOT NULL,
    event_type TEXT NOT NULL,
    service TEXT NULL,
    old_state TEXT NULL,
    new_state TEXT NULL,
    message TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS incidents (
    id TEXT PRIMARY KEY,
    service TEXT NOT NULL,
    title TEXT NOT NULL,
    impact TEXT NOT NULL,
    status TEXT NOT NULL,
    created_at TEXT NOT NULL,
    resolved_at TEXT NULL
);";
        command.ExecuteNonQuery();
    }

    private static OperationsEvent ReadEvent(SqliteDataReader reader)
    {
        return new OperationsEvent(
            reader.GetInt64(0),
            DateTimeOffset.Parse(reader.GetString(1)),
            Enum.Parse<EventType>(reader.GetString(2)),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.GetString(6));
    }
}
