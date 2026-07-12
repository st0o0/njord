## Context

After the stream-pipeline-refactoring and mqtt-egress-streamref changes, the pipeline maps each `FetchOutcome.Success` to a single `MqttMessage` containing one large state JSON per device. The `StatePayloadBuilder` produces one JSON blob; the `PipelineActor` wraps it in an `MqttMessage` and sinks it via StreamRef into the egress MergeHub.

The current state topic scheme is `njord/{location}/{model}/state` with all hourly and daily data in one retained payload. This change splits the output into multiple topics per horizon and adds delta-publishing.

## Goals / Non-Goals

**Goals:**
- One topic per horizon: `njord/{location}/{model}/{horizon}` with flat JSON
- Delta-publishing: skip horizons whose payload hasn't changed since last publish
- Clean upgrade path: tombstone old `/state` retained messages
- Simpler discovery templates

**Non-Goals:**
- Per-parameter topics
- Changing fetch or throttle logic
- Changing connection management

## Decisions

### 1. Topic scheme: `njord/{location}/{model}/{horizon}`

**Choice:** Drop `/state` suffix. The horizon segment (`h3`, `d0`, etc.) is the leaf.

**Why:** Cleaner URLs, the horizon IS the state identity. Wildcards still work: `njord/+/+/h3` gives all models' 3h forecasts for all locations.

### 2. StatePayloadBuilder returns horizon → JSON dictionary

**Choice:** `Dictionary<string, string> BuildPerHorizon(ModelForecast, ResolvedParameterSet, IReadOnlyList<int>, int)` replacing the old single-string `Build`.

**Why:** The builder knows the data structure; it should produce the split. The caller (pipeline) then maps each entry to an `MqttMessage`.

### 3. Delta-publishing via in-memory last-published cache

**Choice:** A `ConcurrentDictionary<(string Location, string ModelId, string Horizon), string>` holds the last-published JSON per slot. Before publishing, compare the new payload string against the cached value. If identical, skip.

**Why over hash-based:**
- String equality is O(n) but payloads are small (~500 bytes per horizon)
- No hash collision risk
- The cache naturally serves as "last known state" for diagnostics

**Cache lifetime:** Lives in the `PipelineActor`. Cleared on actor restart (which causes a full republish — correct behavior after a restart).

### 4. FetchOutcome.Success → multiple MqttMessages (fan-out)

**Choice:** In the pipeline graph, replace the single `Select(toMqttMessage)` with `SelectMany` that emits 0..10 MqttMessages per outcome (one per horizon that changed).

```
FetchOutcome.Success
  → BuildPerHorizon() → Dictionary<horizon, json>
  → Filter(changed only)
  → MapConcat → [MqttMessage, MqttMessage, ...]
```

### 5. Discovery: `state_topic` per component points to horizon topic

**Choice:** Each sensor component in the discovery payload carries:
```json
{
  "state_topic": "njord/lucerne/icon_d2/h3",
  "value_template": "{{ value_json.temperature }}"
}
```

Instead of the old:
```json
{
  "state_topic": "njord/lucerne/icon_d2/state",
  "value_template": "{{ value_json.h3.temperature }}"
}
```

### 6. Tombstone old `/state` topics on startup

**Choice:** On first connect, publish an empty retained message to `njord/{location}/{model}/state` for every configured device. This clears the stale blob from the broker for users upgrading from the old scheme.

**When to remove:** After one release cycle, the tombstone logic can be dropped (nobody will have the old retained messages anymore).

## Risks / Trade-offs

**[Topic count increase]** → 24 devices × 10 horizons = 240 retained topics (up from 24). Mosquitto handles this trivially — not a concern.

**[Cache memory]** → 240 entries × ~500 bytes = ~120KB. Negligible.

**[First cycle after restart publishes everything]** → Cache is empty, so all horizons publish. This is correct — ensures HA has fresh data after a restart.

**[Discovery payload size]** → Each component now has a longer `state_topic` (includes horizon). Minimal impact on the already-large discovery JSON.
