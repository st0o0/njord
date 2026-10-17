## Why

`AdminService.SetEnrichment` validates every touched section first but then writes them one after another, so an I/O failure part-way leaves a half-applied update. Its failures are one `"; "`-joined string, "restart required" hints are mixed into `warnings` next to budget warnings, an empty list cannot be reset to its default, a request with no section silently reports `applied = true`, and clients (ha-njord's options UI) cannot validate without writing. njord and ha-njord release together as 0.5.0, so the response contract can change now.

## What Changes

- **Atomic update**: `IConfigWriter` gets a batch method that validates all touched sections, then persists them in one override-file write with one configuration reload. `ConfigManager` uses it for `UpdateAnalysisConfig`; either every section is applied or none.
- **Structured result (BREAKING, proto)**: `ConfigResponse` gains `repeated ConfigIssue issues` (section + message) and `repeated string restart_required`; restart hints leave `warnings`. `rejection_reason` stays as the human-readable summary.
- **Reset to defaults**: `SetEnrichmentRequest` gains `repeated EnrichmentSection reset`; a listed section's override is removed so the base configuration (appsettings / environment) applies again. Resets are applied before the updates of the same request.
- **Validate only**: `SetEnrichmentRequest.dry_run` validates and returns the result without persisting; `ConfigResponse.dry_run` echoes it.
- **Empty request is rejected** with an issue instead of reporting `applied = true`.

## Non-goals

- Hot reload of the enable toggles (they keep requiring a restart; reported through `restart_required`).
- Changing `SetLocations`, `SetSettings`, `SetBudget` request semantics. They benefit from `restart_required` because the response mapping is shared, but get no `reset`, `dry_run` or `issues` in this change.
- Per-field (below section level) reset or error paths.
- Persistence, polling or budget changes (no API-budget impact).

## Capabilities

### New Capabilities

- `set-enrichment-atomic-update`: SetEnrichment applies all sections or none, supports reset and dry run, and reports structured issues and restart requirements.

### Modified Capabilities

- `grpc-v2-admin-service`: `SetEnrichment` request/response contract and the `IConfigWriter` delegation requirement change.

## Impact

- `protos/njord/v2/admin.proto`: `SetEnrichmentRequest` (new fields 7–8, 6 stays reserved), `ConfigResponse` (new fields 6–8), enums `EnrichmentSection`, message `ConfigIssue`.
- `Njord.Core/Configuration/{IConfigWriter,ConfigWriter}.cs`, `Njord/Configuration/ConfigManager.cs`, `Njord.Messages/Config/` (`UpdateAnalysisConfig`, its responses), `Njord.Grpc/Mapping/ConfigProtoMappings.cs`.
- Tests: `Njord.Core.Tests` (writer batch), `Njord.Tests` (ConfigManager), `Njord.Grpc.Tests` (mapping, service), golden masters.
- Consumer: ha-njord can map `issues` to form fields and use `dry_run`; no ha-njord change is required to keep working, the old fields remain.
- Ordering: apply after `typed-wire-enums` (same `admin.proto` and `ConfigProtoMappings.cs`); both ship in 0.5.0.
