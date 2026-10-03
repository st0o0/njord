## Context

Akka.Persistence.Sql uses Newtonsoft.Json as default serializer. The current snapshot actors persist domain objects directly (`ModelForecast`, `ForecastSeries`, enrichment result records). These types contain:

- `IReadOnlyList<T>` backed by `<>z__ReadOnlyList<T>` (C# collection expression) — no public constructor
- `IReadOnlyDictionary<ParameterDef, double?>` — complex record as dictionary key
- `Dictionary<string, object>` (enrichments) — requires `$type` metadata for polymorphism

All three patterns break Newtonsoft.Json deserialization during snapshot recovery.

## Goals / Non-Goals

**Goals:**
- Snapshot state is serialization-safe: arrays, string-keyed dictionaries, concrete types only.
- Domain model is completely decoupled from persistence representation.
- Existing recovery tests pass with the new DTOs.
- DTO types are simple enough to survive domain model refactoring.

**Non-Goals:**
- Migrating old snapshots (they're already broken; actors restart with empty state and re-poll).
- Custom Akka serializer registration.
- Changing domain types.

## Decisions

### Decision 1: DTO types live in `Njord.Grpc` namespace alongside the actors

**Why:** The DTOs are internal to the persistence layer. They don't belong in the domain. Placing them next to the actors that use them keeps the dependency direction clean: Grpc → Domain, never Domain → Grpc.

### Decision 2: Arrays everywhere, no collections

**Why:** `T[]` is the most serialization-friendly collection type. Newtonsoft.Json, System.Text.Json, protobuf, MessagePack — all handle arrays natively. No constructor issues, no interface ambiguity.

### Decision 3: `ParameterDef` flattened to `string` (ApiName) in DTOs

`ForecastPoint` uses `IReadOnlyDictionary<ParameterDef, double?>`. In the DTO this becomes `Dictionary<string, double?>` keyed by `ParameterDef.ApiName`. On recovery, the ApiName is resolved back to a `ParameterDef` via `ParameterRegistry.GetByApiName()`. Unknown ApiNames (from removed parameters) are silently dropped.

### Decision 4: Enrichment DTOs use a discriminated wrapper instead of `object`

`EnrichmentSnapshotState` stores `Dictionary<string, object>` where values are `AlertResult`, `IndexResult`, etc. The DTO uses a wrapper with a `TypeName` discriminator and a `JsonPayload` string. On save, the enrichment result is serialized to a JSON string with its type name. On recovery, the type name selects the concrete deserialization target.

**Alternative considered:** `$type` metadata in Newtonsoft.Json (`TypeNameHandling.Auto`). Rejected because it's a known security risk and brittle across assembly version changes.

### Decision 5: Mapping is static extension methods

`ToDto()` on domain types, `ToDomain()` on DTO types, as static extension methods in a `SnapshotMapping` class. This avoids polluting either the domain or the DTO types with cross-layer knowledge.

## Risks / Trade-offs

- **[Risk] ParameterDef lookup on recovery**: If a parameter is removed from the registry between save and recovery, the DTO → domain mapping drops that parameter's data silently. This is acceptable — a removed parameter has no use.

- **[Risk] Enrichment JSON double-serialization**: The enrichment DTO serializes the result object to a JSON string, which is then serialized again as part of the snapshot. This is slightly wasteful but avoids all polymorphism issues.

- **[Trade-off] DailyForecastPoint has `DateOnly`**: Newtonsoft.Json handles `DateOnly` poorly by default. The DTO stores it as ISO string (`yyyy-MM-dd`).
