## Context

`ConfigManager.UpdateAnalysisConfigAsync` validates each touched section with `IConfigWriter.Check<T>` and then calls `IConfigWriter.UpdateAsync<T>` once per section. Each `UpdateAsync` is individually atomic (temp file + `File.Move`) but does its own override-file write and `configurationRoot.Reload()`, so a failure between sections leaves earlier sections persisted. Failures are strings; restart hints go into `ConfigResponse.warnings`. `SetBudget` already uses "omit everything to clear the override" as its reset convention.

## Goals / Non-Goals

**Goals:** all-or-nothing multi-section update, structured result, explicit reset and dry run, no silent no-op.

**Non-Goals:** see proposal.

## Decisions

1. **Batch writer API.** Add `IConfigWriter.UpdateManyAsync(IReadOnlyList<ISectionUpdate> updates, CancellationToken)` (each update = options type + `Action<T>` + optional reset flag). Under the existing lock it clones, applies and validates all sections, aborts without writing if any fails, computes all deltas, performs one `PersistAsync` merge into the override file and one `Reload()`. Alternative (keep per-section writes and roll back on failure) rejected: rollback is a second write that can fail too.
2. **Reset means removing the section's override keys**, so the base configuration shows through (consistent with `SetBudget` clearing its override). The reset is part of the same batch write; updates in the same request are applied on top of the reset state. Alternative (write default values explicitly) rejected: it would freeze today's defaults into the override file and hide later default changes.
3. **`EnrichmentSection` proto enum** (`ENRICHMENT_SECTION_UNSPECIFIED = 0`, `CONSENSUS`, `ALERTS`, `DERIVED`, `TRENDS`, `INDICES`) is used for `reset` and for `ConfigIssue.section`. `UNSPECIFIED` in `reset` is rejected as an issue.
4. **`ConfigIssue { EnrichmentSection section = 1; string message = 2; }`**, `ConfigResponse.issues = 6`, `restart_required = 7`, `dry_run = 8`; `SetEnrichmentRequest.reset = 7`, `dry_run = 8` (6 stays reserved, history precedent). `rejection_reason` is still filled (joined messages) so existing clients keep working. `section` is known because validation runs per options type; messages come from the existing validators unchanged.
5. **Restart hints** move from `warnings` to `restart_required` for all mutation RPCs (the mapping helper `Applied(...)` is shared). `warnings` keeps only budget-projection warnings. This is the one change that touches `SetLocations`/`SetSettings`/`SetBudget` responses.
6. **Dry run** executes validation (including the batch apply on clones) but skips persistence and reload; the response has `applied = false`, empty `rejection_reason`, the current `config`, the issues found (empty when valid) and `dry_run = true`. A request is valid iff `issues` is empty.
7. **Empty request** (no section, no reset) is rejected with an issue whose message states that at least one section is required. Alternative (warning only) rejected: a silent success hides client bugs.
8. **Messages stay Akka-free** (`Njord.Messages/Config`): `UpdateAnalysisConfig` gains `Reset` (set of sections) and `DryRun`; `UpdateAnalysisConfigRejected` carries `IReadOnlyList<ConfigIssue>` with a domain-side section enum; the proto mapping converts.

## Risks / Trade-offs

- Resetting removes override keys, which depends on how `PersistAsync` merges deltas today (it only merges) → needs a removal path in the writer; covered by writer tests on a real temp override file.
- `restart_required` moving out of `warnings` breaks clients that parsed warnings → acceptable in 0.5.0; ha-njord does not parse them (to be confirmed in its change).
- Shared `admin.proto` / `ConfigProtoMappings.cs` with `typed-wire-enums` → apply that change first to avoid merge conflicts.
