## Context

The existing `ConsensusEnrichment` computes model consensus at the configured horizon points (default 6 points). The `ConsensusResult` / `ConsensusResult.Compute()` method already accepts an arbitrary list of horizon hours. The hourly consensus reuses this computation with `horizons = [0, 1, 2, ..., N]` where N is dynamically determined.

The MQTT output follows the existing consensus pattern: one retained topic per horizon with a JSON payload containing all parameter medians plus metadata (`_spread`, `_agreement`, `_models_used`). The gRPC output reuses the existing `ConsensusUpdate` proto message.

## Goals / Non-Goals

**Goals:**
- New `HourlyConsensusEnrichment` as `IStatelessEnrichment` producing hourly consensus data.
- Dynamic cutoff: compute only hours where at least 2 models have data.
- Reuse existing `ConsensusResult.Compute()` — no new consensus algorithm.
- Reuse existing `ConsensusUpdate` proto message — no new proto types.
- MQTT Discovery and state payloads for hourly consensus topics.
- gRPC output via `GetEnrichments` and `StreamEnrichments`.
- Default disabled (`Enabled = false`), opt-in via config.

**Non-Goals:**
- Interpolation of 3-hourly model data to fill hourly gaps.
- Replacing the existing horizon-based consensus enrichment.
- New proto message types — the existing `ConsensusUpdate` structure handles arbitrary horizons.

## Decisions

### Decision 1: Reuse `ConsensusResult` and `ConsensusUpdate` proto

The `ConsensusResult.Compute()` method accepts any list of horizons. The `ConsensusUpdate` proto message uses `repeated HorizonConsensus` with a string key (`"h0"`, `"h1"`, ...). Both scale to 73+ entries without structural changes.

The hourly enrichment produces a `ConsensusResult` — same domain type — but with more horizons. The mapper, state builder, and snapshot DTO already handle `ConsensusResult`. Only the enrichment feature class and proto field are new.

### Decision 2: Dynamic horizon range with ≥2 model cutoff

The enrichment iterates `h0, h1, h2, ...` and stops after the last hour where ≥2 models have data. To determine the cutoff without computing full consensus for every possible hour:

1. Scan all models' `ForecastSeries.Points` to find each model's max `ValidAt`.
2. Sort the max horizons descending. The second-largest value is the cutoff (hour where the second-to-last model drops off).
3. Generate `horizons = [0, 1, 2, ..., cutoffHour]`.

This avoids computing consensus for hours beyond the cutoff.

### Decision 3: Filter hours with < 2 available models post-computation

Even within the cutoff range, individual parameters at specific hours may have fewer than 2 values (e.g., a model covers temperature but not wind at a certain hour). The enrichment filters out `HorizonConsensus` entries where `AvailableModels < 2` after computation. This keeps the output consistent: every reported hour has meaningful consensus.

### Decision 4: Separate proto field, not merged with existing consensus

`GetEnrichmentsResponse` gets a new field `ConsensusUpdate hourly_consensus = 9`. `EnrichmentEvent` gets `ConsensusUpdate hourly_consensus = 17`. This keeps the existing horizon-based consensus independent — consumers that only need 6 horizons don't receive 73.

### Decision 5: MQTT topics follow existing consensus pattern

Topic: `{baseTopic}/{location}/hourly-consensus/h{N}` per hour. Same JSON structure as existing consensus topics (`{ "temperature_2m": 22.5, "_spread": 1.2, "_agreement": 0.87, "_models_used": 4 }`). Discovery creates one sensor per parameter per hour — the hourly consensus device can have many components. Since this is opt-in and produces many entities, users enable it knowingly.

## Risks / Trade-offs

- **[Trade-off] Many MQTT topics**: At 48 hours × 9 parameters, that's ~48 retained topics per location. Mosquitto handles this fine but it's a lot of HA entities. The `Enabled = false` default mitigates accidental explosion.

- **[Risk] Computation cost**: Computing consensus for 48+ hours per poll cycle is ~8x more work than 6 horizons. The consensus computation is lightweight (median/mean of 3-9 values), so this is negligible relative to the HTTP fetch time.

- **[Trade-off] MQTT Discovery payload size**: One discovery config per hourly-consensus device with 48×9 = 432 components. This is within HA's limits but large. May warrant a separate discussion about grouping.
