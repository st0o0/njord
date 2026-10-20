## 1. Extend DomainState

- [x] 1.1 Add `LastHash`, `LearnedCycle`, `Phase`, `MissCount`, `LastChanged`, `NextPollAt` to the per-domain state in `PollWorkerState`
- [x] 1.2 Add `PollPhase` enum (Discovery, Steady) if not already existing at domain level
- [x] 1.3 State extensions: `RecordHashChange(domain, hash, now)`, `RecordHashUnchanged(domain)`, `TransitionToSteady(domain, cycle)`, `FallBackToDiscovery(domain)`
- [x] 1.4 Pure state tests for all transitions

## 2. Per-domain timer logic

- [x] 2.1 PollWorker tick evaluates `DuedomainsAt(now)` instead of fetching all domains
- [x] 2.2 Timer interval set to `min(NextPollAt across all domains) - now`
- [x] 2.3 Only due domains get `FetchDomain` sent
- [x] 2.4 Tests for staggered domain polling

## 3. Cycle learning

- [x] 3.1 On hash change: measure interval since last change, store candidate
- [x] 3.2 After 2 consistent intervals (within 20% tolerance): transition to Steady
- [x] 3.3 In Steady: `NextPollAt = lastPoll + learnedCycle - buffer(30s)`
- [x] 3.4 After 5 misses in Steady: fall back to Discovery
- [x] 3.5 Tests for cycle learning and fallback

## 4. Persistence

- [x] 4.1 Extend `PersistedDomainState` with Phase, LearnedCycle, MissCount, LastHash fields
- [x] 4.2 Update persistence mappings
- [x] 4.3 Recovery test: learned cycles restored from journal

## 5. Telemetry

- [x] 5.1 Per-domain poll metric with domain tag
- [x] 5.2 Cycle-learned event metric

## 6. Validation

- [x] 6.1 `dotnet build Njord.slnx`
- [x] 6.2 Pipeline tests pass
- [x] 6.3 Persistence roundtrip tests pass
