# Persistence

njord uses Akka.Persistence to store each location's learned poll schedule and the request budget usage. Two storage providers are supported.

## Configuration

```json
{
  "Njord": {
    "Persistence": {
      "Provider": "Sqlite"
    },
    "Storage": {
      "PersistencePath": "data/njord.db"
    }
  }
}
```

## Providers

### SQLite (default)

The default provider stores data in a local SQLite file. No external database required.

| Option | Default | Description |
|--------|---------|-------------|
| `Provider` | `"Sqlite"` | Storage provider |
| `Storage:PersistencePath` | `"data/njord.db"` | Path to the SQLite database file (relative to working directory) |

In Docker, mount a volume at `/app/data` to persist the database across container restarts:

```bash
docker run -v njord-data:/app/data ghcr.io/st0o0/njord:latest
```

### PostgreSQL

For larger deployments or when you already run PostgreSQL, use the `PostgreSql` provider.

```json
{
  "Njord": {
    "Persistence": {
      "Provider": "PostgreSql",
      "ConnectionString": "Host=db;Database=njord;Username=njord;Password=secret"
    }
  }
}
```

| Option | Default | Description |
|--------|---------|-------------|
| `Provider` | `"PostgreSql"` | Storage provider |
| `ConnectionString` | `null` | PostgreSQL connection string (required when using PostgreSql) |

::: warning
When using PostgreSQL, the `ConnectionString` is required. njord will refuse to start without it.
:::

## When is persistence needed?

Persistence is required for:
- **Poll worker state**: each location's PollWorker remembers its learned update cycle and next poll across restarts
- **Budget usage**: keeping the recorded API calls (and the usage counted from them) across restarts
