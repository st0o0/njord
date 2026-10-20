## 1. Extend DomainState

- [ ] 1.1 Add `LastHash`, `LearnedCycle`, `Phase`, `MissCount`, `LastChanged`, `NextPollAt` to the per-domain state in `PollWorkerState`
- [ ] 1.2 Add `PollPhase` enum (Discovery, Steady) if not already existing at domain level
- [ ] 1.3 State extensions: `RecordHashChange(domain, hash, now)`, `RecordHashUnchanged(domain)`, `TransitionToSteady(domain, cycle)`, `FallBackToDiscovery(domain)`
- [ ] 1.4 Pure state tests for all transitions

## 2. Per-domain timer logic

- [ ] 2.1 PollWorker tick evaluates `DuedomainsAt(now)` instead of fetching all domains
- [ ] 2.2 Timer interval set to `min(NextPollAt across all domains) - now`
- [ ] 2.3 Only due domains get `FetchDomain` sent
- [ ] 2.4 Tests for staggered domain polling

## 3. Cycle learning

- [ ] 3.1 On hash change: measure interval since last change, store candidate
- [ ] 3.2 After 2 consistent intervals (within 20% tolerance): transition to Steady
- [ ] 3.3 In Steady: `NextPollAt = lastPoll + learnedCycle - buffer(30s)`
- [ ] 3.4 After 5 misses in Steady: fall back to Discovery
- [ ] 3.5 Tests for cycle learning and fallback

## 4. Persistence

- [ ] 4.1 Extend `PersistedDomainState` with Phase, LearnedCycle, MissCount, LastHash fields
- [ ] 4.2 Update persistence mappings
- [ ] 4.3 Recovery test: learned cycles restored from journal

## 5. Telemetry

- [ ] 5.1 Per-domain poll metric with domain tag
- [ ] 5.2 Cycle-learned event metric

## 6. Validation

- [ ] 6.1 `dotnet build Njord.slnx`
- [ ] 6.2 Pipeline tests pass
- [ ] 6.3 Persistence roundtrip tests pass
