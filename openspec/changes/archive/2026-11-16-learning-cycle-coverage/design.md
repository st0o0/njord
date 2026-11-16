## Context

The learning cycle is a per-domain state machine (`DomainPollState`) driven by `DomainPollStateExtensions`. Each domain of each location independently learns its API update interval by observing hash changes. The PollWorker actor persists `DomainPolled` events and reconstructs state on recovery.

Existing test coverage:
- **State tests** (`PollWorkerStateTests`, `DomainPollStateTests`): 25+ facts covering most transitions, but 5 gaps in the transition matrix.
- **Actor tests** (`PollWorkerPollingTests`): 11 facts covering startup, changed/unchanged, 3-changes-to-Steady, triggers, restarts. No Steady-mode timing or miss-fallback tests.
- **gRPC**: `GetStatus` returns one `ModelStatus` per location with an aggregated phase -- no per-domain visibility.

## Goals / Non-Goals

**Goals:**
- 100% transition coverage in state tests (every cell of the Discovery/Steady/Suspended x Changed/Unchanged/NoData matrix)
- Actor-level proof that Steady scheduling uses `LearnedCycle - SteadyBuffer`, not DiscoveryInterval
- Actor-level proof that 5-miss fallback works end-to-end through the message flow
- Per-domain poll state in `GetStatus` proto response for E2E observability

**Non-Goals:**
- Changing the learning algorithm
- E2E test steps that exercise real learning transitions (follow-up work)
- Exposing domain states in ha-njord

## Decisions

### 1. State tests: add 5 targeted Facts to existing test classes

Add to `PollWorkerStateTests`:
- `A_consistent_change_in_steady_stays_steady` -- uses `Steady()` fixture, adds a 4th change at +3h
- `An_inconsistent_change_in_steady_falls_back_to_discovery` -- uses `Steady()`, adds change at +1h
- `A_no_data_event_in_steady_transitions_to_suspended` -- uses `Steady()`, applies NoData
- `A_no_data_event_in_suspended_stays_suspended` -- chains two NoData events

Add to `DomainPollStateTests`:
- `Tolerance_boundary_rejects_intervals_just_outside_twenty_percent` -- 10h candidate, 12.01h interval

**Rationale**: These are pure state-function tests, fast and deterministic. They complete the transition matrix without duplicating existing coverage.

### 2. Actor tests: 2 new Facts in PollWorkerPollingTests

- `A_steady_domain_polls_at_the_learned_cycle_minus_buffer` -- Start, 3 changes at 1h intervals to reach Steady, advance FakeTimeProvider by 59m30s, assert FetchDomain arrives.
- `Five_misses_in_steady_fall_back_to_discovery_through_the_actor` -- Start, reach Steady, trigger 5 polls with same hash, assert final report shows Discovery phase.

**Rationale**: The actor tests validate that the state-machine transitions actually drive the timer and message flow correctly. FakeTimeProvider allows deterministic time control.

### 3. Proto: `DomainPollStatus` message in `ModelStatus`

```protobuf
message DomainPollStatus {
  string domain = 1;
  PollPhase phase = 2;
  optional int64 cycle_seconds = 3;
  int32 miss_count = 4;
  google.protobuf.Timestamp next_poll = 5;
}

message ModelStatus {
  // ... existing fields ...
  repeated DomainPollStatus domain_states = 7;
}
```

**Rationale**: Additive proto change (wire-compatible). The field number 7 is the next available on `ModelStatus`. Uses `optional int64` for `cycle_seconds` so Discovery domains (no learned cycle) omit it. The `PollPhase` enum already exists and includes Suspended (need to add it to the proto enum).

### 4. Data flow: PollStateEntry gains per-domain states

`PollStateEntry` currently holds one aggregated phase. Extend it with `IReadOnlyList<DomainPollStateEntry> DomainStates` where `DomainPollStateEntry` is a new record `(string Domain, PollPhase Phase, long? CycleSeconds, int MissCount, DateTimeOffset? NextPollAt)`. The PollManager builds this from the PollWorkerReport's domain-level data. `StatusMappingExtensions.ToModelStatus` maps each entry to a `DomainPollStatus` proto.

## Risks / Trade-offs

- **Proto enum gap**: The existing proto `PollPhase` has Discovery and Steady but not Suspended. Adding `POLL_PHASE_SUSPENDED = 2` is additive but needs the mapping extended. Old clients ignore unknown enum values.
- **PollStateEntry size**: Adding per-domain data to PollStateEntry increases the message size per location. With 6 domains this is ~6 small records -- negligible.
- **Actor test flakiness**: Steady-mode actor tests depend on FakeTimeProvider advancing precisely. Use exact advances (not ranges) and generous ExpectMsg timeouts.
