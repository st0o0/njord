## Context

The polling lifecycle grew organically: Discovery polls fast to learn update cycles, Steady polls at the learned cycle, Suspended handles NoData. The ContentHash fix (content-hash-change-detection) revealed that most domains never leave Discovery because their data rarely changes within the discovery window. The result: excessive API calls, unpredictable budget, and a state machine that is hard to reason about.

Open-Meteo documents its update frequencies per model. We can derive good defaults from these. The ContentHash already handles the "did data change" question. What remains is scheduling, which should be configuration-driven.

## Goals / Non-Goals

**Goals**: Replace the 3-state learning cycle with a 2-state (Active/Suspended) model. Per-domain configurable intervals with sensible defaults. Lightweight adaptive drift so the system self-corrects within safe bounds. Predictable budget projection.

**Non-Goals**: Hot-reloading intervals. Changing the Gateway/Pipeline/FeatureEngine. Changing the gRPC API surface beyond PollPhase values.

## Decisions

### Per-domain intervals live in PollingOptions, not in separate domain options

**Choice**: `Njord:Polling:Domains:<Domain>:PollInterval` in PollingOptions.
**Alternative**: Put intervals in each domain's own options (ForecastOptions, MarineOptions, etc.).
**Rationale**: Polling intervals are a polling concern. Forecast/Marine options describe API request parameters (days, variables). Keeping them in PollingOptions makes BudgetGuard validation straightforward and avoids cross-project references.

### Defaults in a static DomainDefaults class in Core

**Choice**: `DomainDefaults.PollInterval(DomainKind)` as an exhaustive switch in Njord.Core.
**Alternative**: Defaults as property initializers in PollingOptions.
**Rationale**: The defaults are domain-specific knowledge, not config-binding defaults. A switch in Core makes them discoverable and testable. PollingOptions consumes them when no override is configured.

### Drift is in-memory only, not persisted

**Choice**: Drift observations and the effective interval are held in `DomainPollState` in memory. After restart, drift resets to zero.
**Alternative**: Persist drift state.
**Rationale**: Drift is a soft optimization. Persisting it adds schema complexity and risks stale drift surviving long outages. The system re-converges within a few poll cycles after restart. Keeps persistence records simple.

### Drift uses an exponential moving average

**Choice**: Track `ObservedInterval` as an EMA of consecutive change intervals. When a data change is detected: `ema = alpha * observed + (1 - alpha) * ema`, with `alpha = 0.3`. The effective interval is `clamp(ema, configured * 0.5, configured * 2.0)`. Drift kicks in after 3 observations.
**Alternative**: Simple average of last N intervals.
**Rationale**: EMA adapts faster to recent changes and forgets old data naturally. Alpha=0.3 means the most recent observation has 30% weight, providing responsiveness without instability. The clamp prevents drift from exceeding safe bounds.

### PollPhase enum: Active replaces Discovery and Steady

**Choice**: `PollPhase { Active, Suspended }`. Proto: `POLL_PHASE_ACTIVE = 1`. Rename `POLL_PHASE_DISCOVERY = 1` to `POLL_PHASE_ACTIVE = 1` (same wire value). Remove `POLL_PHASE_STEADY = 2`. Keep `POLL_PHASE_SUSPENDED = 3`.
**Alternative**: Keep all three phases but make Discovery and Steady behave identically.
**Rationale**: Two distinct names for the same behavior is confusing. Clean break at 0.x. Reusing wire value 1 means ha-njord sees Active where it used to see Discovery, which is semantically correct (both mean "polling normally").

### DiscoveryInterval removed from PollWorkerState and persistence

**Choice**: Remove `DiscoveryInterval` from `PollWorkerState`, `PersistedPollWorkerState`, and `PollingStarted`. Each domain carries its own interval. PollManager passes the per-domain interval map to StartPolling.
**Alternative**: Keep DiscoveryInterval but ignore it.
**Rationale**: Dead fields in persistence records invite confusion. Breaking change is acceptable at 0.x.

## Risks / Trade-offs

[Risk: Defaults are wrong for a region or model combination] The documented update frequencies are global averages. Some model/region combinations may update faster or slower. Mitigation: adaptive drift will auto-correct within 0.5x-2.0x, and the operator can override per domain.

[Risk: Drift converges to wrong value during transient API issues] If the API temporarily delivers stale data, drift may lengthen the interval inappropriately. Mitigation: clamp at 2.0x prevents excessive drift, and restart resets drift entirely.

[Risk: Breaking persistence change requires volume wipe] Mitigation: document in release notes. At 0.x this is expected. The system recovers fully from an empty journal.

## Migration Plan

1. Implement the new model (Active/Suspended, per-domain intervals, drift)
2. Update persistence records (breaking: remove learning fields, add PollInterval)
3. Update proto (rename DISCOVERY to ACTIVE, remove STEADY)
4. Update tests (replace learning cycle tests with interval+drift tests)
5. Update BudgetGuard and PollingOptionsValidator
6. Update Config Builder budget formula
7. Release with note: "Volume delete required after upgrade"

## Open Questions

None - the design was validated through explore-mode discussion and the constraints are well-understood.
