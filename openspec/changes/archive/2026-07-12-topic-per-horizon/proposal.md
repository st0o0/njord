## Why

The current state topic (`njord/{location}/{model}/state`) carries a single monolithic JSON with ~240 values (30 hourly parameters × 6 horizons + 15 daily parameters × 4 day-offsets). This is aesthetically "klumpig", scales poorly (each new horizon or parameter makes the blob bigger), and forces a full republish of all values every cycle even when most haven't changed. Near horizons (h3, h6) update every cycle; far horizons (h48, h72) only change when the model produces a new run (every 6-12h); daily values change at most once per day. The monolith wastes bandwidth, bloats the HA recorder, and is harder to reason about.

## What Changes

- **Split state into one topic per horizon**: `njord/{location}/{model}/{horizon}` (e.g. `h3`, `h6`, `h12`, `h24`, `h48`, `h72`, `d0`, `d1`, `d2`, `d3`). Each topic carries a flat JSON with only the parameter values for that time-slice (~30 keys for hourly, ~15 for daily).
- **Drop the `/state` suffix** from state topics — the horizon segment IS the state discriminator.
- **Delta-publishing**: after building each horizon's payload, compare against the last-published payload for that horizon. Only publish if values differ. This naturally reduces MQTT traffic to the horizons that actually changed.
- **Update Discovery `state_topic` and `value_template`**: each component points to its specific horizon topic. Templates simplify from `{{ value_json.h3.temperature }}` to `{{ value_json.temperature }}`.
- **Update `StatePayloadBuilder`**: instead of building one large JSON, produce a dictionary of horizon → flat JSON. The pipeline maps these to individual `MqttMessage` instances.
- **Tombstone old `/state` topics on startup** to clean retained stale blobs from the broker for users upgrading.

## Non-goals

- Changing the discovery payload format beyond `state_topic`/`value_template` adjustments.
- Per-parameter topics (Option C from exploration — too many topics).
- Adding MQTT command sources.
- Changing the availability topic structure.
- API budget changes (no change to polling — same fetch volume, just different publishing granularity).

## Capabilities

### New Capabilities
- `delta-publishing`: Compare each horizon payload against last-published; skip unchanged horizons. Maintain an in-memory cache of last-published payload per (device, horizon) for comparison.

### Modified Capabilities
- `mqtt-egress`: The state topic scheme changes from one topic per device to one topic per (device, horizon). Discovery `state_topic` and `value_template` fields update accordingly. Old `/state` topics are tombstoned on startup.

## Impact

- **`src/Njord/Egress/TopicScheme.cs`**: `StateTopic` method changes signature and output (now includes horizon segment, drops `/state`).
- **`src/Njord/Egress/StatePayloadBuilder.cs`**: Produces `Dictionary<string, string>` (horizon → JSON) instead of one big JSON string.
- **`src/Njord/Egress/DiscoveryPayloadBuilder.cs`**: `state_topic` per component points to the horizon-specific topic; `value_template` simplified.
- **`src/Njord/Pipeline/PipelineActor.cs`**: Maps one `FetchOutcome.Success` to multiple `MqttMessage` instances (one per horizon) and applies delta-publishing filter.
- **`src/Njord.Tests/`**: `TopicSchemeSpec`, `StatePayloadBuilderSpec`, `DiscoveryPayloadBuilderSpec`, pipeline specs updated.
- **Dependencies**: None new.
