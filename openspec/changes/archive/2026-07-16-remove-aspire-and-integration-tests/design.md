## Context

The solution currently has 7 projects. Four of them are Aspire/integration-test infrastructure:

```
Njord.slnx
├── Njord/                          ← stays
├── Njord.Tests/                    ← stays
├── Njord.Tests.Shared/             ← stays
├── Njord.AppHost/                  ← delete
├── Njord.ServiceDefaults/          ← delete (inline into Njord/)
├── Njord.Tests.Integration/        ← delete
└── Njord.Tests.Integration.E2E/    ← delete
```

`Njord.ServiceDefaults` contains two methods used by `Njord/`:
- `AddNjordTelemetry()` — Serilog configuration (called in `Program.cs`)
- `MapDefaultEndpoints()` — `/healthz` + `/alive` endpoints (called in `NjordApplicationSetup.cs`)

These are pure ASP.NET/Serilog setup with no Aspire dependency.

## Goals / Non-Goals

**Goals:**
- Remove all Aspire orchestration and integration test projects.
- Keep the Serilog and health endpoint behavior intact by inlining `ServiceDefaults` logic.
- Clean solution down to 3 projects: `Njord`, `Njord.Tests`, `Njord.Tests.Shared`.

**Non-Goals:**
- Redesigning the test strategy.
- Removing production Docker artifacts (`Dockerfile`, `docker-compose.example.yml`).

## Decisions

### D1: Inline `ServiceDefaults` methods into `Njord/Configuration/`

Move `AddNjordTelemetry` and `MapDefaultEndpoints` into a new static class in `Njord/Configuration/HostExtensions.cs` (or inline directly into `Program.cs` and `NjordApplicationSetup.cs`). Since both methods are small (~10 lines each), inlining into `NjordApplicationSetup` and `Program.cs` respectively is the simplest path — no new file needed.

### D2: Delete project directories entirely

Use `rm -rf` on the four project directories after removing them from the solution. The `bin/obj` folders go with them.

### D3: Clean up `Directory.Packages.props`

Remove `Aspire.Hosting.PostgreSQL` and `Aspire.Hosting.Testing` package versions — no remaining project references them.

### D4: Update `CLAUDE.md`

Remove the integration and E2E test `dotnet run` commands. Keep the unit test command.

## Risks / Trade-offs

- **[No integration tests]** → Accepted. Unit tests (501) cover actor behavior, domain logic, and stream graphs. Integration tests will be rebuilt later with a cleaner approach.
- **[Aspire specs become orphaned]** → Specs are removed as part of this change. They can be recreated when Aspire is re-added.
