## Why

The gRPC proto definitions are out of sync with the domain model after the daily-slice-indices change: `IndexUpdate` still has a flat single-score layout while the domain now produces per-day score sets. Additionally, all proto files carry `reserved` fields and comments from the energy removal that add noise without value — since njord is pre-release, there are no deployed clients requiring wire compatibility.

## What Changes

- **BREAKING**: `IndexUpdate` redesigned from flat score fields to `repeated DayScoreSet days` with per-day scores, envelopes, and `hours_included`. Frost and VPD extracted into dedicated sub-messages.
- **BREAKING**: `ventilation` field removed, replaced by `night_ventilation` in `DayScoreSet`.
- **BREAKING**: All `reserved` fields and field numbers removed across all proto files (`hdd`/`cdd` in IndexUpdate, `energy` in GetEnrichmentsResponse/EnrichmentEvent/DetailedEnrichmentConfig/SetEnrichmentRequest, `heating_base_temp`/`cooling_base_temp` in IndexConfig). Field numbers renumbered cleanly.
- All "energy removed" comments cleaned up.
- `EnrichmentProtoMapper.MapIndices` rewritten to iterate `Days` list and map envelopes, frost, and VPD using the new sub-messages.
- New proto messages: `DayScoreSet`, `ScoreEnvelope`, `FrostInfo`, `VpdInfo`.

## Non-goals

- No new gRPC RPCs or services.
- No changes to non-index enrichment messages (alerts, trends, derived, history, consensus).
- No API-budget impact — this change is proto/mapper only, no polling changes.

## Capabilities

### New Capabilities

_(none)_

### Modified Capabilities

- `grpc-v2-common`: `IndexUpdate` message redesigned to daily slices with new sub-messages; all reserved fields removed; energy comments removed.
- `grpc-v2-weather-service`: `GetEnrichmentsResponse` and `EnrichmentEvent` reserved energy fields removed.
- `grpc-v2-admin-service`: `DetailedEnrichmentConfig`, `SetEnrichmentRequest`, and `IndexConfig` reserved fields removed; `IndexConfig` heating/cooling base temp fields removed.
- `grpc-enrichment-api`: `EnrichmentProtoMapper.MapIndices` rewritten for daily-slice `IndexResult`; `StreamEnrichments` index update scenario updated.

## Impact

- **Proto files**: `protos/njord/v2/common.proto`, `weather.proto`, `admin.proto` — breaking wire format changes.
- **Generated code**: All C# proto classes in `Njord.Grpc.V2` regenerated on build.
- **Mapper**: `src/Njord/Grpc/EnrichmentProtoMapper.cs` — `MapIndices` rewritten.
- **gRPC services**: `WeatherGrpcService.cs` — no code changes needed (mapper handles the shape change).
- **Tests**: `EnrichmentProtoMapperSpec`, any test constructing `IndexUpdate` directly.
