## 1. State-level test gaps

- [x] 1.1 Add `A_consistent_change_in_steady_stays_steady` to PollWorkerStateTests: Steady() + Changed at +3h → Phase=Steady, LearnedCycle=3h
- [x] 1.2 Add `An_inconsistent_change_in_steady_falls_back_to_discovery` to PollWorkerStateTests: Steady() + Changed at +1h → Phase=Discovery, LearnedCycle=null, CandidateCycle=1h
- [x] 1.3 Add `A_no_data_event_in_steady_transitions_to_suspended` to PollWorkerStateTests: Steady() + NoData → Phase=Suspended, NextPollAt=+24h
- [x] 1.4 Add `A_no_data_event_in_suspended_stays_suspended` to PollWorkerStateTests: Started() + NoData + NoData → Phase=Suspended, NextPollAt=second NoData+24h
- [x] 1.5 Add `Tolerance_boundary_rejects_intervals_just_outside_twenty_percent` to DomainPollStateTests: 10h candidate + 12.01h interval → stays Discovery

## 2. Actor-level timing tests

- [x] 2.1 Add `A_steady_domain_polls_at_the_learned_cycle_minus_buffer` to PollWorkerPollingTests: 3 changes at 1h intervals → Steady, advance FakeTimeProvider by 59m30s, assert FetchDomain arrives
- [x] 2.2 Add `Five_misses_in_steady_fall_back_to_discovery_through_the_actor` to PollWorkerPollingTests: reach Steady, trigger 5 unchanged responses, assert final report Phase=Discovery

## 3. Proto: per-domain poll status

- [x] 3.1 Add `POLL_PHASE_SUSPENDED = 2` to the PollPhase enum in njord.proto
- [x] 3.2 Add `DomainPollStatus` message to njord.proto (domain, phase, optional cycle_seconds, miss_count, next_poll)
- [x] 3.3 Add `repeated DomainPollStatus domain_states = 7` to ModelStatus in njord.proto

## 4. Domain data in PollStateEntry

- [x] 4.1 Add `DomainPollStateEntry` record to Njord.Messages.Polling (Domain, Phase, CycleSeconds, MissCount, NextPollAt)
- [x] 4.2 Extend `PollStateEntry` with `IReadOnlyList<DomainPollStateEntry> DomainStates`
- [x] 4.3 Update `PollWorkerMessageMappingExtensions` to populate DomainStates from PollWorkerReport domain-level data
- [x] 4.4 Update `StatusMappingExtensions.ToModelStatus` to map DomainStates → repeated DomainPollStatus proto
- [x] 4.5 Extend `StatusMappingExtensions.ToProto(PollPhase)` to handle PollPhase.Suspended → POLL_PHASE_SUSPENDED

## 5. Tests for proto mapping

- [x] 5.1 Add StatusMappingTests for per-domain poll state mapping (DomainPollStateEntry → DomainPollStatus proto)
- [x] 5.2 Add StatusMappingTests for Suspended phase mapping
- [x] 5.3 Run all test projects, verify 0 failures
