## Why

The v2 redesign left dead code and misplaced types behind. AlertComputed, IWithTargetId, and EnrichmentKind are unused. ServiceInfoMappings still has multi-model catalog logic from the consensus era. CatalogLocation duplicates coordinates already on Location. StreamWiringMessages mixes three unrelated concerns in one file.

## What Changes

- Delete dead PipelineEvent variant `AlertComputed` (replaced by `AlertRaised`)
- Delete `IWithTargetId` interface (replaced by `IWithLocationId`)
- Delete or collapse `EnrichmentKind` enum (only 1 member: `Alerts`)
- Move `PollFailureReason` from Njord.Model to Njord.Messages (polling concern, not vocabulary)
- Fix `ServiceInfoMappings`: remove stale multi-model resolution, use Location coordinates from config
- Fix `CatalogLocation`: remove redundant Latitude/Longitude fields (Location already carries them)
- Split `StreamWiringMessages.cs` into per-owner files (Pipeline, Forecast, Grpc handshakes)
- Delete `ForecastValue` if unused after Forecast project removal

## Non-goals

- Restructuring projects (that's split-domains-into-projects).

## Capabilities

### New Capabilities
_(none)_

### Modified Capabilities
- `pipeline-composition`: Dead AlertComputed variant removed
- `domain-forecast-model`: PollFailureReason relocated

## Impact

- **Njord.Messages**: Dead types removed, PollFailureReason added
- **Njord.Model**: PollFailureReason removed, ForecastValue checked
- **Njord.Core**: StreamWiringMessages split
- **Njord (host)**: ServiceInfoMappings simplified
- **Tests**: Updated for moved/removed types
