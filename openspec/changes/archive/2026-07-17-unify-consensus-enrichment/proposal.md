## Why

There are now two consensus enrichments (`ConsensusEnrichment` and `HourlyConsensusEnrichment`) that do the same thing with different horizon inputs. The horizon-based consensus is a strict subset of the hourly consensus — anyone wanting h3/h6/h12 can get it from the hourly output. Maintaining two enrichments with identical computation, mapping, proto messages, and MQTT patterns is unnecessary duplication.

## What Changes

- Remove `ConsensusEnrichment` and its config toggle (`ConsensusOptions`).
- Rename `HourlyConsensusEnrichment` to `ConsensusEnrichment` with `TypeName = "consensus"`.
- The enrichment computes consensus hourly from h0 to the dynamic cutoff (≥2 models), same as the current hourly implementation.
- MQTT publishes one topic per hour (`{baseTopic}/{location}/consensus/h{N}`).
- gRPC uses the single `consensus` field in `GetEnrichmentsResponse` and `EnrichmentEvent` (field numbers 8 and 16). Remove the `hourly_consensus` fields (9 and 17).
- Config simplified: `Consensus.Enabled` toggles the single enrichment (default `true` to match current behaviour). Remove `HourlyConsensusOptions`.
- Update Discovery payload to use dynamic hourly horizons.

## Non-goals

- Changing the consensus computation algorithm.
- Adding configurable horizon subsets for MQTT filtering.
- No API-budget impact — no polling changes.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `consensus-computation`: No spec changes — the computation is unchanged.
- `hourly-consensus`: Absorbs the old horizon-based consensus. TypeName becomes `"consensus"`, config uses existing `ConsensusOptions`.

## Impact

- Removed: `src/Njord/Enrichment/Features/ConsensusEnrichment.cs`
- Modified: `src/Njord/Enrichment/Features/HourlyConsensusEnrichment.cs` → renamed to `ConsensusEnrichment.cs`, TypeName changed to `"consensus"`
- Modified: `src/Njord/Configuration/EnrichmentOptions.cs` — remove `HourlyConsensusOptions`, keep `ConsensusOptions` with `Enabled = true`
- Modified: `src/Njord/Configuration/NjordServiceSetup.cs` — single registration
- Modified: `protos/njord/v1/forecast_service.proto` — remove `hourly_consensus` fields
- Modified: `src/Njord/Grpc/EnrichmentProtoMapper.cs` — remove `"hourly-consensus"` case
- Modified: `src/Njord/Grpc/ForecastGrpcService.cs` — remove `HourlyConsensus` case
- Modified: `src/Njord/Mqtt/StatePayloadBuilder.cs` — remove `topicSegment` parameter (always `"consensus"`)
- Tests updated accordingly
