## Why

A detailed comparison against the sibling FunkArr project found that njord's Persistence
and Messages projects have significant structural gaps that FunkArr avoids entirely:

**Persistence:**
- njord DTOs are **mutable classes with public setters**; FunkArr uses sealed immutable
  records throughout.
- All 5 root DTOs carry `[JsonProperty("v")] public int Version { get; set; } = 1` but
  **no recovery code ever checks it** — dead versioning infrastructure violating AGENTS.md's
  own "Recovery code must handle all versions ≥ 1" rule.
- Multi-type files (`BudgetTrackerDtos.cs` = 2-4 DTOs per file); FunkArr uses file-per-type.
- Flat directory; FunkArr organizes by domain (`Events/Download/`, `Events/ScoringHistory/`).

**Messages:**
- njord has ~15 files with multi-type consolidation (e.g. `SchedulerMessages.cs` = 9 types,
  `SnapshotMessages.cs` = 16 types); FunkArr has ~114 files, file-per-type.
- Inconsistent response naming: mix of `*Found`/`*NotFound`, `*Result`/`*Failed` — FunkArr
  consistently uses `*Completed`/`*Failed`.
- Split message definition: `QueryHistory` in Njord.Messages, its response hierarchy
  (`QueryHistoryResult`/`QueryHistoryFailed`) in `Njord.Enrichment/ForecastHistoryMessages.cs`.
- `TriggerImmediatePollResult` has no abstract base (breaking the query→response pattern).
- `DataChanged` record nested inside `SchedulerActor.cs` instead of in Messages/Persistence.
- `SubscribeInbound` record inside `MqttConnectionActor.cs`.

## What Changes

**Persistence:**
- Convert all DTOs from mutable classes to sealed records (with `{ get; init; }` for
  Newtonsoft.Json compatibility).
- Either implement version checking in all 5 `FromPersistence`/`ToDomain` methods (if
  versioning is actually wanted) or remove the dead `Version` fields entirely. Given both
  projects are 0.x, FunkArr's pragmatic "no versioning" is the recommended approach.
- Split multi-type files into file-per-type.
- Add subdirectory organization by domain (`Pipeline/`, `Enrichment/`, `Grpc/`).

**Messages:**
- Move `QueryHistoryResponse`/`QueryHistoryResult`/`QueryHistoryFailed` into
  `Njord.Messages/Enrichment/EnrichmentMessages.cs` (reunite query + response).
- Standardize response naming: adopt `*Completed`/`*Failed` consistently (rename `*Found`
  → `*Completed`, `*NotFound` → `*Failed` or `*NotAvailable`).
- Add abstract base to `TriggerImmediatePollResult`.
- Move `DataChanged` out of `SchedulerActor.cs` into `Njord.Persistence/Pipeline/`.
- Move `SubscribeInbound` out of `MqttConnectionActor.cs` into `Njord.Messages/Mqtt/`.
- Split large message files into file-per-command where feasible.

## Capabilities

### New Capabilities

- `persistence-record-hygiene`: sealed immutable records, file-per-type, domain subdirectories,
  no dead versioning infrastructure.
- `message-naming-consistency`: `*Completed`/`*Failed` response naming, no split definitions,
  abstract-base response hierarchies, no actor-nested messages.

### Modified Capabilities

None. All changes are internal restructuring; no wire-format or API contract changes. The
persistence DTO `[JsonProperty]` names stay identical (only the C# class→record shape
changes; Newtonsoft.Json deserialization handles `{ get; init; }` records).

## Impact

- `src/Njord.Persistence/` (all files)
- `src/Njord.Messages/` (all files)
- `src/Njord.Enrichment/ForecastHistoryMessages.cs` (moved to Messages)
- `src/Njord.Pipeline/SchedulerActor.cs` (`DataChanged` extracted)
- `src/Njord.Mqtt/MqttConnectionActor.cs` (`SubscribeInbound` extracted)
- All projects that reference renamed response types (mechanical find/replace).
- All `*DtoMapping.cs` files (adapt to record constructors).
- Test projects: `Njord.Persistence.Tests/` (Verify snapshots re-approved if needed).

## Non-goals

- Not changing `[JsonProperty]` string keys — extend-only rule stays.
- Not changing the Messages project's references to `Njord.Weather`/`Njord.Sensors`.
- Not moving `StreamWiringMessages.cs`/`StreamShutdownMessages.cs` out of Core (the
  "Phase 2 will remove these" comment suggests they're deliberately temporary).
- Not introducing persisted enums (lower priority; strings work).

## API budget impact

None.
