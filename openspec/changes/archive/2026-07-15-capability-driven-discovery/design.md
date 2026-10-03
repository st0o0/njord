## Context

Njord publishes one HA device per (location, model) with a static grid of (parameter × horizon) sensor components. The grid is derived purely from configuration (parameter groups × horizon list × day offsets) and ignores two realities: (1) models have different forecast ranges — `icon_d2` covers ~48 h, `meteoswiss_icon_ch1` ~33 h, while `ecmwf_ifs025` reaches 240 h — and (2) models don't all support every parameter (e.g. `icon_d2` lacks `precipitation_probability`). The result: hundreds of permanently unavailable sensors in HA, cluttered dashboards, and wasted MQTT bandwidth.

Additionally, `sunrise`/`sunset` daily parameters are always null because `timeformat=unixtime` returns them as unix timestamps (numbers), but `MapDaily` uses `as string` to cast the raw `JsonElement`, which yields null for non-string JSON values.

The static `ModelCoverageRegistry` already knows each model's `MaxForecastHours`. What's missing is parameter-level knowledge, which can only come from observing actual API responses.

## Goals / Non-Goals

**Goals:**

- Eliminate permanently unavailable sensors by publishing discovery only for (parameter × horizon) combinations a model actually supports.
- Use `MaxForecastHours` from the existing registry to statically cap horizons per model — no API call needed, no learning delay.
- Dynamically learn parameter support from the first successful fetch and communicate it to the discovery layer.
- Strip null-valued keys from state JSON payloads for compactness.
- Fix sunrise/sunset to produce ISO 8601 timestamps in state payloads.

**Non-Goals:**

- Consensus computation — remains deferred.
- Enrichment device filtering — enrichment features define their own fixed component sets.
- Changing poll interval, request budget, or API call weight.
- Tombstoning stale discovery configs from prior runs.
- Per-model parameter configuration (e.g. requesting different parameter sets per model) — all models get the same active parameter set from config; the filtering happens at discovery/egress time based on what came back non-null.

## Decisions

### D1: Static horizon capping via ModelCoverageRegistry

**Decision:** `HorizonProjection.BuildPerHorizon` and `DiscoveryPayloadBuilder.Build` receive the model's `MaxForecastHours` and exclude horizons beyond it. Daily day-offsets are capped at `ceil(MaxForecastHours / 24)`.

**Rationale:** The data already exists in the registry. No learning delay, no API probing. A model's maximum forecast range is a stable property that changes only when the provider extends it (which would require a registry update anyway).

**Alternatives considered:**
- Learn horizon support dynamically like parameters → unnecessary complexity; model ranges are well-documented and stable.
- Keep all horizons but suppress discovery for null ones after first fetch → horizon data is deterministic from the model's range, no need to wait for a fetch.

### D2: Dynamic parameter learning in ModelStateActor

**Decision:** `ModelStateActor` maintains a `HashSet<ParameterDef>` per (location, model) of parameters that have been observed with at least one non-null value. After the first successful `FetchOutcome.Success`, it sends a `ModelCapabilityLearned` message to the `DiscoveryActor` with the set of supported parameters and the applicable horizons (capped by `MaxForecastHours`).

On subsequent fetches, if a previously-null parameter appears with a non-null value, the actor sends an updated `ModelCapabilityLearned` to trigger incremental discovery (late registration of new sensors).

**Rationale:** Parameter support can only be known from actual API responses. The actor already processes every `FetchOutcome.Success` — adding a set-union check is minimal overhead. The message goes to `DiscoveryActor` via actor reference (resolved from DI/registry), keeping the architecture clean.

**Alternatives considered:**
- Central capability service/singleton → violates "actors for lifecycle" guardrail; scattered state.
- Learn in the client itself → client is a pure HTTP mapper; capability state doesn't belong there.
- Hard-code parameter support per model → fragile, would require ongoing maintenance as Open-Meteo evolves.

### D3: Deferred discovery with timeout

**Decision:** `DiscoveryActor` does NOT publish discovery on first connect. Instead, it collects `ModelCapabilityLearned` messages from all expected (location, model) pairs. Once all have reported (or a configurable timeout expires, default 2× poll interval), it publishes discovery for the union of learned capabilities.

On HA birth (`homeassistant/status` = `online`), it re-publishes with the current learned state.

On timeout, any (location, model) pairs that haven't reported are skipped — their discovery will be published when their `ModelCapabilityLearned` arrives later (late join).

**Rationale:** Publishing discovery before knowing capabilities produces the exact problem we're solving. The timeout ensures the system degrades gracefully if a model is temporarily unavailable (those sensors simply don't appear until the model's first successful fetch). The first poll cycle typically completes in seconds (8 models × throttled requests), so the user rarely sees the delay.

**Alternatives considered:**
- Publish full grid on connect, then tombstone unsupported sensors after learning → complex, requires tracking what was published vs. what should be; tombstoning retained messages is error-prone.
- Two-phase discovery (provisional then final) → confusing for HA, creates entity churn.

### D4: Null-key stripping in HorizonProjection

**Decision:** `HorizonProjection.BuildPerHorizon` omits keys with null values from the JSON object for each horizon. The existing behavior of skipping entire horizons with no data at all is preserved.

**Rationale:** A horizon that has temperature data but no precipitation_probability should emit `{"temperature": 15.2}` instead of `{"temperature": 15.2, "precipitation_probability": null}`. This reduces payload size and aligns with the availability-template approach (the key's absence triggers `unavailable` in HA via the `is not none` check).

### D5: sunrise/sunset unix-to-ISO conversion

**Decision:** In `OpenMeteoClient.MapDaily`, when processing a parameter with `ValueType == TimeString` and the raw JSON value is a number (not a string), convert it from a unix timestamp to ISO 8601 UTC string (`DateTimeOffset.FromUnixTimeSeconds(n).ToString("O")`).

**Rationale:** Open-Meteo returns sunrise/sunset as unix timestamps when `timeformat=unixtime`. The current code uses `raw.GetString()` which returns null for numbers. The fix belongs in the client (ingest layer) because it's a deserialization concern — the domain model expects string values for `TimeString` parameters.

### D6: ModelCapabilityLearned message design

**Decision:** The message is:
```csharp
sealed record ModelCapabilityLearned(
    string Location,
    WeatherModel Model,
    IReadOnlySet<ParameterDef> SupportedParameters,
    IReadOnlyList<int> ApplicableHorizons,
    IReadOnlyList<int> ApplicableDayOffsets);
```

It carries the full learned state (not a delta), making it idempotent. The `DiscoveryActor` can replace its knowledge for that (location, model) pair on every receipt without tracking history.

**Rationale:** Full-state messages are simpler to reason about than deltas. The set is small (≤60 parameters, ≤10 horizons) so the overhead is negligible. Idempotency means the `DiscoveryActor` can handle restarts, re-deliveries, and late joins uniformly.

## Risks / Trade-offs

**[Risk] First-cycle sensors appear with a delay** → On startup, HA sensors don't exist until the first poll cycle completes and capabilities are learned (~seconds with default config). Mitigation: the delay is brief and the alternative (permanent unavailable sensors) is worse. The timeout ensures discovery proceeds even if some models are slow.

**[Risk] Parameter appears after first fetch (late learning)** → A parameter that was null on the first fetch but non-null on a subsequent fetch triggers an incremental discovery update. Mitigation: `ModelCapabilityLearned` can be sent multiple times; `DiscoveryActor` merges and re-publishes. HA handles new components in an existing device gracefully.

**[Risk] MaxForecastHours in registry becomes stale** → If Open-Meteo extends a model's range, njord won't publish those extra horizons until the registry is updated. Mitigation: model ranges change rarely; when they do, it's a one-line registry update. This is strictly better than the current situation where those horizons show unavailable.

**[Risk] HA entity IDs change for users upgrading** → Sensors that were permanently unavailable will disappear. Mitigation: this is the intended improvement. Document in release notes.

**[Trade-off] Increased actor complexity in ModelStateActor** → Tracking parameter sets adds state. Mitigated by keeping it as a simple `HashSet<ParameterDef>` with set-union semantics — no complex state machine.

**[Trade-off] Discovery depends on data flow** → Discovery is no longer fire-and-forget on connect; it requires data to arrive first. Mitigated by the timeout fallback and the fact that the poll pipeline starts immediately on service start.

## Open Questions

None — the approach is decided and all component interactions are defined.
