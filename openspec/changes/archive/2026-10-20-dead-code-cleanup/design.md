## Context

Post-v2 redesign cleanup. Multiple types survived the transition but are no longer referenced.

## Goals / Non-Goals

**Goals:** Remove dead code, fix misplaced types, simplify stale mappings.
**Non-Goals:** Project restructuring, new features.

## Decisions

### D1: Delete, don't deprecate
Dead code is deleted outright. No `[Obsolete]` markers — the code has no external consumers.

### D2: StreamWiringMessages → one file per handshake owner
`PipelineStreamMessages.cs`, `ForecastStreamMessages.cs`, `GrpcStreamMessages.cs` in Njord.Core/Actors/.

### D3: CatalogLocation uses Location directly
Remove separate Lat/Lon fields. `CatalogLocation(Location Location, IReadOnlyList<string> AvailableModels)`.
