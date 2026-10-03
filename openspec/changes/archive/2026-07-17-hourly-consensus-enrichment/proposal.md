## Why

The existing consensus enrichment computes model agreement only at the configured horizon points (default +3/+6/+12/+24/+48/+72h). For time-series visualization (charts, graphs) and fine-grained decision making, hourly consensus data is needed. The consensus computation infrastructure already supports arbitrary horizons — the gap is a dedicated enrichment feature that iterates every hour and dynamically determines where consensus is meaningful (at least 2 models available).

## What Changes

- Add a new `HourlyConsensusEnrichment` implementing `IStatelessEnrichment` with `TypeName = "hourly-consensus"`.
- The enrichment computes consensus for every hour from h0 up to the last hour where at least 2 models have data. Hours with fewer than 2 models are excluded from the output.
- Add `HourlyConsensusOptions` to `EnrichmentOptions` with `Enabled = false` (opt-in, produces significantly more data than the horizon-based consensus).
- Add gRPC proto messages for hourly consensus and map them in `EnrichmentProtoMapper`.
- Add MQTT state output: one retained topic per hour (same pattern as the existing consensus — one topic per horizon, but hourly).
- Add MQTT Discovery payload for hourly consensus sensors.
- Register the new enrichment in DI.

## Non-goals

- Replacing the existing horizon-based consensus — both coexist independently.
- Interpolating 3-hourly model data to fill gaps between their native data points. Models contribute only at hours where they have actual data (the existing ±30min tolerance window applies).
- Changing polling frequency or API requests. No API-budget impact — this enrichment consumes the existing `ModelSnapshot`, no additional API calls.

## Capabilities

### New Capabilities

- `hourly-consensus`: Hourly consensus enrichment with dynamic cutoff based on model availability.

### Modified Capabilities

(none)

## Impact

- New: `src/Njord/Enrichment/Features/HourlyConsensusEnrichment.cs`
- New: `src/Njord/Configuration/EnrichmentOptions.cs` — add `HourlyConsensusOptions`
- New: proto messages for hourly consensus in the `.proto` file
- Modified: `src/Njord/Configuration/NjordServiceSetup.cs` — register new enrichment
- Modified: `src/Njord/Grpc/EnrichmentProtoMapper.cs` — add hourly consensus mapping
- Modified: `src/Njord/Mqtt/StatePayloadBuilder.cs` — add `FromHourlyConsensus` method
- Modified: `src/Njord/Grpc/SnapshotDtos.cs` — add `HourlyConsensusResult` to known enrichment types
- Tests for the new enrichment computation and the dynamic cutoff logic
