## Why

The current polling lifecycle (Discovery/Steady/Suspended) was designed to learn each domain's API update frequency by observing data changes. The ContentHash fix exposed a fundamental flaw: domains whose data rarely changes (Marine, Ensemble, Seasonal) stay in Discovery indefinitely because the system never observes two consecutive changes to learn a cycle. During a 12h soak test, AirQuality/Marine/Seasonal/Ensemble stayed in POLL_PHASE_DISCOVERY for 3+ hours, polling every 5 minutes and burning budget on unchanged data. Meanwhile, the actual update frequencies of Open-Meteo APIs are well-documented and largely stable (Forecast: 1-3h, AirQuality: 12-24h, Marine: 6-24h, Flood: daily, Seasonal: daily).

The learning approach conflates two orthogonal concerns: *when* to poll (scheduling) and *whether data changed* (dispatch). ContentHash already handles dispatch correctly. Scheduling should be configuration-driven with sensible defaults, not learned from observation.

## What Changes

- **BREAKING**: Replace the 3-state learning cycle (Discovery/Steady/Suspended) with a 2-state model (Active/Suspended). Remove DiscoveryInterval, CandidateCycle, LearnedCycle, MissCount-for-cycling, IsConsistent, TransitionToSteady, FallBackToDiscovery.
- Each domain gets its own PollInterval with defaults derived from documented API update frequencies: Forecast=60m, AirQuality=60m, Marine=6h, Ensemble=6h, Seasonal=24h, Flood=24h.
- Operators can override per-domain intervals via `Njord:Polling:Domains:<Domain>:PollInterval`.
- The global `Njord:Polling:PollInterval` becomes a floor: no domain polls faster than this.
- Add lightweight adaptive drift: track observed change intervals, and if data consistently changes faster or slower than the configured interval, drift the effective interval toward reality. Clamped to `configured * [0.5, 2.0]` so drift cannot run away.
- BudgetGuard projects usage from actual per-domain intervals instead of a single global interval.
- Failure handling (exponential backoff, rate-limit delay, NoData suspension) is unchanged.
- ContentHash change detection is unchanged.

## Non-goals

- Removing per-domain independent timers (the PollWorker architecture stays).
- Changing the Gateway, Pipeline, or FeatureEngine (they are downstream of polling).
- Supporting hot-reload of polling intervals (stays startup-only via IOptions).
- Changing the gRPC GetStatus/TriggerPoll API surface (DomainPollStatus fields may change but the RPCs stay).

## Capabilities

### New Capabilities

- `per-domain-poll-intervals`: Per-domain configurable polling intervals with defaults derived from API update frequencies, replacing the single global PollInterval for scheduling.
- `adaptive-poll-drift`: Lightweight drift mechanism that adjusts effective poll intervals toward observed data change frequency, clamped to a safe range around the configured value.

### Modified Capabilities

- `learning-cycle-tests`: The learning cycle is removed entirely. This spec is superseded by per-domain-poll-intervals and adaptive-poll-drift.
- `nodata-domain-suspension`: Suspension behavior stays but the re-entry target changes from Discovery to Active.
- `poll-failure-backoff`: Backoff stays but the recovery target changes from Discovery to Active, and the cap is the domain's own PollInterval instead of the global one.
- `content-hash-change-detection`: Purpose text changes (no longer "enabling the learning cycle"), requirements unchanged.
- `per-domain-telemetry`: Phase labels change (Discovery/Steady removed, Active added).

## Impact

- `src/Njord.Core/Configuration/PollingOptions.cs` - add DomainIntervals, remove DiscoveryInterval
- `src/Njord.Core/Polling/DomainDefaults.cs` - new: default intervals per domain
- `src/Njord.Polling/DomainPollState*.cs` - simplified state, remove learning fields
- `src/Njord.Polling/DomainPollStateExtensions.cs` - rewrite: remove learning logic, add drift
- `src/Njord.Polling/PollWorker.cs` - pass per-domain interval instead of DiscoveryInterval
- `src/Njord.Polling/PollWorkerState*.cs` - remove DiscoveryInterval from state
- `src/Njord.Polling/PollManager*.cs` - pass per-domain intervals instead of DiscoveryInterval
- `src/Njord.Polling/Configuration/BudgetGuard.cs` - project from per-domain intervals
- `src/Njord.Polling/Configuration/PollingOptionsValidator.cs` - validate per-domain intervals
- `src/Njord.Persistence/Events/Polling/*` - breaking schema change (remove learning fields, add PollInterval)
- `src/Njord.Grpc/Mapping/StatusMappingExtensions.cs` - map new phase enum
- `protos/njord/njord.proto` - PollPhase enum: remove DISCOVERY, add ACTIVE (or reuse DISCOVERY as ACTIVE)
- `src/Njord.Polling.Tests/` - rewrite learning tests to interval+drift tests
- `docs/data/features.json` / Config Builder - update budget projection formula
- No API-budget impact from the change itself; the new defaults will *reduce* budget usage vs Discovery-phase polling
