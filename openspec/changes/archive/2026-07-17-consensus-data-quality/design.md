## Context

The consensus pipeline already filters per parameter × per horizon (only horizons with ≥2 models for a given parameter are included). The `ConsensusResult` data structure is correct — each `ParameterConsensus` has its own `ByHorizon` map with per-parameter `AvailableModels`. The problem is in the MQTT serialization layer (`StatePayloadBuilder.FromConsensus`) which flattens all parameters into one JSON per horizon and takes metadata from the first parameter only.

## Goals / Non-Goals

**Goals:**
- MQTT consensus payload carries `_models_used` per parameter so consumers know confidence per value.
- Spec documents daily forecast filtering (all-null entries dropped at ingest).
- All registry and daily-filter fixes from this session are committed and documented.

**Non-Goals:**
- Changing the consensus computation algorithm.
- Per-parameter `_spread` and `_agreement` (these are computed from the first parameter's cross-model spread, which is a reasonable proxy).

## Decisions

### Decision 1: Per-parameter `_models_used` in MQTT JSON

Current payload:
```json
{"temperature_2m": 22.5, "precipitation_probability": null, "_spread": 1.2, "_models_used": 4}
```

New payload:
```json
{
  "temperature_2m": 22.5,
  "temperature_2m_models": 4,
  "precipitation_probability": null,
  "precipitation_probability_models": 0,
  "_spread": 1.2,
  "_agreement": 0.87
}
```

Each parameter value gets a companion `{param}_models` field. A consumer can check `temperature_2m_models >= 2` before trusting the value. The global `_models_used` is removed — it was misleading.

`_spread` and `_agreement` remain global (from the reference parameter, typically temperature) — they indicate overall model agreement at this horizon, not per-parameter.

### Decision 2: Daily filtering is ingest-layer, not spec-layer

The all-null daily filter (`HasAnyValue` check in `MapDaily`) is an ingest concern — it prevents creating `DailyForecastPoint` entries that carry no information. This is documented in a new spec but requires no further implementation (already fixed).

## Risks / Trade-offs

- **[Trade-off] Payload size increase**: Each parameter adds a `_models` companion field. For 9 parameters, that's 9 extra small integers. Negligible.
- **[Risk] Breaking change for HA consumers**: Existing template sensors that use `_models_used` will break. Mitigated by documenting the change and the new field names.
