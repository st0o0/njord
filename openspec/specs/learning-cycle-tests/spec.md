# learning-cycle-tests Specification

## Purpose

Test coverage for the per-domain learning cycle state machine (Discovery → Steady → Suspended). Ensures every transition in the state matrix is verified at both state-function and actor levels.

## Requirements

### Requirement: State-level tests cover every transition in the learning cycle state machine
The test suite SHALL verify every cell in the Discovery/Steady/Suspended x Changed/Unchanged/NoData/Failed transition matrix. Each transition SHALL have at least one dedicated test with deterministic timestamps.

#### Scenario: Steady domain receiving a consistent change stays Steady
- **WHEN** a domain is in Steady phase with a LearnedCycle of 3 hours
- **AND** a hash change arrives 3 hours after the last change (within 20% tolerance)
- **THEN** the domain SHALL remain in Steady phase with LearnedCycle preserved

#### Scenario: Steady domain receiving an inconsistent change falls back to Discovery
- **WHEN** a domain is in Steady phase with a LearnedCycle of 3 hours
- **AND** a hash change arrives 1 hour after the last change (outside 20% tolerance)
- **THEN** the domain SHALL transition to Discovery phase with LearnedCycle cleared and a new CandidateCycle of 1 hour

#### Scenario: Steady domain receiving NoData transitions to Suspended
- **WHEN** a domain is in Steady phase
- **AND** a NoData event is received
- **THEN** the domain SHALL transition to Suspended phase with a 24-hour recheck interval

#### Scenario: Suspended domain receiving another NoData stays Suspended
- **WHEN** a domain is in Suspended phase
- **AND** a NoData event is received at the 24-hour recheck
- **THEN** the domain SHALL remain in Suspended phase with a new 24-hour recheck scheduled

#### Scenario: CycleTolerance boundary rejects intervals outside 20%
- **WHEN** a domain has a CandidateCycle of 10 hours
- **AND** a hash change arrives after 12.01 hours (20.1% drift, outside tolerance)
- **THEN** the domain SHALL stay in Discovery with a new CandidateCycle of 12.01 hours

### Requirement: Actor-level tests verify timing-critical Steady behavior
The PollWorker actor tests SHALL verify that the Steady phase uses the learned cycle for scheduling and that miss-based fallback triggers correctly through the actor message flow.

#### Scenario: Steady poll timer fires at LearnedCycle minus SteadyBuffer
- **WHEN** a PollWorker has learned a 1-hour cycle for a domain
- **AND** time advances by 59 minutes 30 seconds (1 hour minus 30s SteadyBuffer)
- **THEN** the worker SHALL send a FetchDomain request to the gateway

#### Scenario: Five consecutive misses in Steady cause fallback through the actor
- **WHEN** a PollWorker is in Steady phase for all enabled domains
- **AND** 5 consecutive TriggerLocationPoll + unchanged-hash responses are processed
- **THEN** the PollWorkerReport SHALL show phase=Discovery and the next poll SHALL be scheduled at DiscoveryInterval
