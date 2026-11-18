## 1. Core: Domain defaults and options

- [x] 1.1 Add `DomainDefaults` static class in `Njord.Core/Polling/` with `PollInterval(DomainKind)` returning the per-domain defaults (Forecast=60m, AQ=60m, Marine=6h, Ensemble=6h, Seasonal=24h, Flood=24h)
- [x] 1.2 Add `DomainPollingOverride` record and `DomainIntervals` dictionary to `PollingOptions`. Remove `DiscoveryInterval`
- [x] 1.3 Add a `ResolvedInterval(DomainKind)` method on `PollingOptions` that resolves: per-domain override > DomainDefaults > floor at PollInterval
- [x] 1.4 Update `PollingOptionsValidator` to validate per-domain overrides (positive, max 24h) and remove DiscoveryInterval validation
- [x] 1.5 Add unit tests for `DomainDefaults`, `ResolvedInterval`, and validator changes (covered in DomainPollStateTests + PollingOptionsValidatorTests)

## 2. State model: Active/Suspended

- [x] 2.1 Change `PollPhase` enum: replace `Discovery`/`Steady` with `Active`. Keep `Suspended`
- [x] 2.2 Simplify `DomainPollState`: remove `CandidateCycle`, `LearnedCycle`. Keep `LastHash`, `LastChanged`, `NextPollAt`, `ConsecutiveFailures`. Add `PollInterval` (the domain's resolved interval)
- [x] 2.3 Rewrite `DomainPollStateExtensions`: remove `TransitionToSteady`, `FallBackToDiscovery`, `IsConsistent`. Simplify `RecordHashChange` (update hash+lastChanged, schedule next at effective interval). Simplify `RecordHashUnchanged` (schedule next at effective interval, no MissCount cycling logic)
- [x] 2.4 Add drift fields to `DomainPollState`: `ObservedIntervalEma` (TimeSpan?), `DriftObservationCount` (int). These are in-memory only (not persisted)
- [x] 2.5 Add `EffectiveInterval` computed property: if drift has converged (count >= 3), clamp EMA to [configured * 0.5, configured * 2.0]; otherwise return configured interval
- [x] 2.6 In `RecordHashChange`, update drift EMA when `LastChanged` is set: `ema = 0.3 * observed + 0.7 * ema`

## 3. PollWorker and PollManager

- [x] 3.1 Remove `DiscoveryInterval` from `PollWorkerState`. Each domain carries its own `PollInterval`
- [x] 3.2 Update `PollWorkerStateExtensions.Apply(PollingStarted)`: initialize each domain's PollInterval from the event (which carries per-domain intervals from PollManager)
- [x] 3.3 Update `PollManager`: pass per-domain intervals (from `PollingOptions.ResolvedInterval`) in `StartPolling` instead of a single DiscoveryInterval
- [x] 3.4 Update `PollWorker.ScheduleDomain`: use the domain's `EffectiveInterval` for scheduling
- [x] 3.5 Update `FailureBackoff.Delay`: accept the domain's interval as cap instead of the global PollInterval

## 4. Persistence

- [x] 4.1 Update `PersistedPollPhase`: map `Discovery`(0) and `Steady`(1) both to `Active` on read. Write only `Active`(0) and `Suspended`(2)
- [x] 4.2 Update `PersistedDomainState`: remove `CandidateCycle`, `LearnedCycle`. Add `PollInterval` (nullable for backward compat). Keep `MissCount` (still used for failure tracking, just not for cycle learning)
- [x] 4.3 Update `PersistedPollWorkerState`: remove `DiscoveryInterval` (nullable for backward compat read, not written)
- [x] 4.4 Update `PollWorkerPersistenceMappingExtensions`: map new fields, handle legacy records. Use `.ToList()` for DomainStates
- [x] 4.5 Update `PollingStarted` event: replace `DiscoveryInterval` with per-domain intervals (or just the domains list; intervals are config-derived)
- [x] 4.6 Update wire-format tests and roundtrip tests for the new schema

## 5. BudgetGuard

- [x] 5.1 Update `BudgetGuard.Violation` to compute `cyclesPerMonth` per domain using `PollingOptions.ResolvedInterval(domain)` instead of a single `PollInterval`
- [x] 5.2 Update BudgetGuard tests for per-domain projection (existing tests pass with new BudgetGuard)

## 6. gRPC and proto

- [x] 6.1 Update `PollPhase` enum in `njord.proto`: rename `POLL_PHASE_DISCOVERY = 1` to `POLL_PHASE_ACTIVE = 1`, remove `POLL_PHASE_STEADY = 2`, keep `POLL_PHASE_SUSPENDED = 3`
- [x] 6.2 Update `StatusMappingExtensions` for the new phase mapping
- [x] 6.3 Update gRPC mapping tests

## 7. Tests

- [x] 7.1 Rewrite `DomainPollStateTests` (was learning-cycle state tests): test Active scheduling, drift EMA convergence, drift clamping, Suspended transitions
- [x] 7.2 Rewrite `PollWorkerPollingTests` actor tests: verify per-domain timer scheduling, drift reflected in timing
- [x] 7.3 Update `PollWorkerStateTests`: remove learning-cycle transition tests, add per-domain interval tests
- [x] 7.4 Update `PollWorkerFailureTests`: backoff cap uses domain interval
- [x] 7.5 Update `PollManagerStartupTests` / `PollManagerCycleTests` / `PollManagerQueryTests` / `PollManagerTriggerTests` / `PollManagerStateTests`: per-domain intervals
- [x] 7.6 Run all test projects, verify 0 failures (1799 tests, 0 failures)

## 8. Config Builder and docs

- [x] 8.1 Update `docs/.vitepress/theme/builder/budget.ts` to use per-domain intervals in budget formula
- [x] 8.2 Update `docs/data/features.json` if any polling-related metadata changed (no changes needed)
- [x] 8.3 Update `docker-compose.example.yml` and soak compose: remove DiscoveryInterval, add example per-domain overrides
