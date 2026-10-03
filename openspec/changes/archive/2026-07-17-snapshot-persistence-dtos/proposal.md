## Why

Snapshot recovery fails in production with `Newtonsoft.Json.JsonSerializationException: Unable to find a constructor to use for type <>z__ReadOnlyList`. The root cause: domain objects are persisted directly as snapshot state. `ForecastSeries` uses `Points = [.. items]` (C# collection expression) which creates a compiler-generated `<>z__ReadOnlyList<T>` — a type without public constructor that Newtonsoft.Json cannot deserialize. Additionally, `ForecastPoint` uses `IReadOnlyDictionary<ParameterDef, double?>` where `ParameterDef` is a complex record used as dictionary key, and `EnrichmentSnapshotState` stores `Dictionary<string, object>` requiring `$type` metadata for polymorphic deserialization.

## What Changes

- Introduce dedicated persistence DTO types for snapshot state, decoupled from domain model types.
- DTOs use only serialization-safe primitives: arrays (not `IReadOnlyList`), `Dictionary<string, T>` with string keys (not `ParameterDef`), concrete types (not `object`).
- `ForecastSnapshotActor` maps domain → DTO on `SaveSnapshot`, DTO → domain on `Recover<SnapshotOffer>`.
- `EnrichmentSnapshotActor` maps domain → DTO on `SaveSnapshot`, DTO → domain on `Recover<SnapshotOffer>`.
- Update recovery tests to verify round-trip through the new DTOs.

## Non-goals

- Changing the Akka.Persistence serializer from Newtonsoft.Json to something else (protobuf, System.Text.Json). The DTOs are serializer-agnostic and would survive a future migration.
- Migrating existing persisted snapshots. Old snapshots are already broken; the fix is to let the actors start with empty state and re-poll (which happens automatically).
- Changing domain model types (no `ForecastSeries`, `ForecastPoint`, `ParameterDef` changes).
- No API-budget impact — no polling changes.

## Capabilities

### New Capabilities

- `snapshot-persistence-dtos`: Dedicated DTO types for snapshot serialization with mapping to/from domain objects.

### Modified Capabilities

- `snapshot-actors`: Snapshot save/recover uses DTOs instead of domain objects directly.

## Impact

- New file: `src/Njord/Grpc/SnapshotDtos.cs` — DTO types and mapping extensions.
- Modified: `src/Njord/Grpc/ForecastSnapshotActor.cs` — replace `ForecastSnapshotState` with DTO-based state.
- Modified: `src/Njord/Grpc/EnrichmentSnapshotActor.cs` — replace `EnrichmentSnapshotState` with DTO-based state.
- Modified: `src/Njord.Tests/Grpc/ForecastSnapshotRecoverySpec.cs` — tests verify DTO round-trip.
- Modified: `src/Njord.Tests/Grpc/EnrichmentSnapshotRecoverySpec.cs` — tests verify DTO round-trip.
