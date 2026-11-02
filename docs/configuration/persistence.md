# Persistence

njord uses Akka.Persistence to store each location's learned poll schedule and the request budget usage. Two storage providers are supported.

## Configuration

| Option | Default | Description |
|--------|---------|-------------|
| `ConnectionString` | `null` | PostgreSQL connection string; when set, njord uses PostgreSQL instead of SQLite |
| `DataPath` | `"data/njord.db"` | Path to the SQLite database file (relative to working directory) |

The provider is selected automatically: if `ConnectionString` is set, njord uses PostgreSQL; otherwise it uses SQLite with the `DataPath`.

### SQLite (default)

No external database required. Data is stored in a local SQLite file.

```json
{
  "Njord": {
    "Persistence": {
      "DataPath": "data/njord.db"
    }
  }
}
```

In Docker, mount a volume at `/app/data` to persist the database across container restarts:

```bash
docker run -v njord-data:/app/data ghcr.io/st0o0/njord:latest
```

### PostgreSQL

For larger deployments or when you already run PostgreSQL, set a connection string:

```json
{
  "Njord": {
    "Persistence": {
      "ConnectionString": "Host=db;Database=njord;Username=njord;Password=secret"
    }
  }
}
```

## When is persistence needed?

Persistence is required for:
- **Poll worker state**: each location's PollWorker remembers its learned update cycle and next poll across restarts
- **Budget usage**: keeping the recorded API calls (and the usage counted from them) across restarts
