## Context

FunkArr.Persistence: zero references, all sealed records, no Version fields, file-per-type,
subdirectory organization by domain, separate persisted enums for wire stability.
FunkArr.Messages: zero references, ~114 files (file-per-type), consistent `*Completed`/
`*Failed` naming, domain subdirectories, no split definitions.

njord has the opposite in both projects (detailed in the proposal).

## Goals / Non-Goals

**Goals:**
- Persistence DTOs are sealed immutable records with `{ get; init; }`.
- Dead `Version` fields removed (pragmatic, matching FunkArr's 0.x approach).
- File-per-type + domain subdirectories in both Persistence and Messages.
- Consistent `*Completed`/`*Failed` response naming in Messages.
- No split query/response definitions; no actor-nested messages.

**Non-Goals:**
- Not changing `[JsonProperty]` string keys.
- Not introducing persisted enums for string-serialized values.
- Not moving stream-wiring messages out of Core (marked temporary).
- Not changing Messages' references to Weather/Sensors projects.

## Decisions

1. **Sealed records with `{ get; init; }`** for all persistence DTOs. Newtonsoft.Json can
   deserialize `{ get; init; }` properties via its parameterless-constructor fallback
   (all DTOs already have one implicitly via `= 1`/`= 0` defaults). Verified in the
   existing `Njord.Persistence.Tests` roundtrip specs.
   Alternative: positional records with `[JsonConstructor]`. Rejected — more invasive,
   and `[JsonProperty]` attribute placement on positional parameters is awkward with
   Newtonsoft.

2. **Remove Version fields entirely** rather than implementing version checking. Both
   projects are 0.x; no deployed system has Version > 1; adding version checks now would
   be theater-on-theater. If versioning becomes needed at 1.0+, it's a deliberate new
   capability, not a retrofit of dead infrastructure.

3. **Response naming migration**: `ForecastFound` → `QueryForecastCompleted`,
   `ForecastNotAvailable` → `QueryForecastFailed`, `SensorSnapshotFound` →
   `QuerySensorSnapshotCompleted`, `SensorSnapshotNotFound` →
   `QuerySensorSnapshotFailed`. All call sites updated mechanically (find/replace + build).

4. **Move `DataChanged`** to `Njord.Persistence/Pipeline/DataChangedDto.cs` (it's a
   persistence event used in `Persist(dto, ...)`). Move `SubscribeInbound` to
   `Njord.Messages/Mqtt/MqttMessages.cs`.

5. **Migration order**: Persistence first (fewer dependents), then Messages (more
   mechanical renames, but straightforward with build-guided find/replace).

## Risks / Trade-offs

- [Risk] Renaming response types is a large mechanical change touching many files. →
  Mitigated by: build-guided (compiler errors guide every missing rename); one rename per
  commit; each commit independently buildable.
- [Risk] Converting mutable classes to `{ get; init; }` records could break Newtonsoft
  deserialization if any DTO relies on post-construction setter mutation. → Mitigated by:
  existing `Njord.Persistence.Tests` roundtrip specs cover all DTOs; run them after each
  conversion.
- [Risk] Removing `Version` fields changes the serialized JSON shape (the `"v": 1` field
  disappears from newly persisted events/snapshots). → Existing data with `"v": 1` is
  harmless (Newtonsoft ignores unknown properties by default); new data without `"v"` is
  fine for recovery code that never checked it. No migration needed.

## Migration Plan

One persistence DTO at a time, then one message-file rename at a time. Each step verified
by build + test run. No deployment coordination needed — pure internal restructuring.

## Open Questions

None.
