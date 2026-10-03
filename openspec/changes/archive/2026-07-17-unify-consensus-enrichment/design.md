## Context

After the `hourly-consensus-enrichment` change, we have two consensus enrichments:
1. `ConsensusEnrichment` — horizons from config (default [3,6,12,24,48,72]), TypeName `"consensus"`
2. `HourlyConsensusEnrichment` — hourly h0..hN with ≥2 model cutoff, TypeName `"hourly-consensus"`

Both call `ConsensusResult.Compute()`, produce `ConsensusUpdate` proto, and publish MQTT topics with the same JSON structure. The only differences are the horizon input and the topic/type names.

## Goals / Non-Goals

**Goals:**
- Single consensus enrichment computing hourly with dynamic cutoff.
- TypeName stays `"consensus"` (the original name — existing MQTT consumers and gRPC clients don't break).
- Config stays `ConsensusOptions` with `Enabled = true` default.
- Remove all `hourly-consensus` naming and the duplicate enrichment class.

**Non-Goals:**
- Configurable subset of hours for MQTT output.
- Changing the consensus algorithm.

## Decisions

### Decision 1: Keep TypeName `"consensus"`, not `"hourly-consensus"`

The original `ConsensusEnrichment` used `"consensus"` — this is what existing MQTT consumers and snapshot state keys reference. By keeping this name, existing data flows survive the change. The `"hourly-consensus"` type was only just introduced and has no production consumers yet.

### Decision 2: Rename the file, don't create a new one

`HourlyConsensusEnrichment.cs` becomes `ConsensusEnrichment.cs` (replacing the old one). The class is renamed to `ConsensusEnrichment`. This is a file rename + content edit, not a new file.

### Decision 3: Revert `StatePayloadBuilder.FromConsensus` signature

The `topicSegment` parameter was added for the hourly variant. With a single enrichment using `"consensus"` as topic segment, the parameter can be removed and the hardcoded `"consensus"` restored.

### Decision 4: Revert proto to single field

Remove `hourly_consensus` fields (9 and 17) from the proto. The existing `consensus` fields (8 and 16) carry the hourly data. Since the `hourly_consensus` fields were just added and have no deployed consumers, this is safe.

## Risks / Trade-offs

- **[Risk] Existing MQTT topics change from 6 horizon topics to N hourly topics**: HA entities that subscribed to `consensus/h3` will still receive data (h3 is included in hourly). But new topics (h0, h1, h2, h4, h5, ...) appear. Discovery will create sensors for all hours.
  **Mitigation**: This is expected behaviour — the user opted in to the enrichment. HA won't break because new topics are simply new entities.
