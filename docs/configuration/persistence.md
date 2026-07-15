# Persistence

njord uses Akka.Persistence to store scheduler state and forecast history. Two storage providers are supported.

## Configuration

```json
{
  "Njord": {
    "Persistence": {
      "Provider": "Sqlite"
    },
    "PersistencePath": "data/njord-journal.db"
  }
}
```

## Providers

### SQLite (default)

The default provider stores data in a local SQLite file. No external database required.

| Option | Default | Description |
|--------|---------|-------------|
| `Provider` | `"Sqlite"` | Storage provider |
| `PersistencePath` | `"data/njord-journal.db"` | Path to the SQLite database file (relative to working directory) |

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
- **History enrichment** — tracking forecast accuracy over time
- **Scheduler state** — remembering poll cycle progress across restarts

If you disable the History enrichment and do not need restart resilience, persistence is optional but still recommended.
