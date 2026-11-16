## Why

The per-domain learning cycle (Discovery → Steady → Suspended) is the core scheduling algorithm that makes njord polite to the Open-Meteo free tier. State-level unit tests cover most transitions, but five gaps remain in the transition matrix, actor-level tests cover only the happy path (3 changes → Steady), and the gRPC GetStatus API exposes only a single aggregated phase per location -- making it impossible for E2E or soak tests to observe individual domain learning transitions.

## What Changes

- Fill 5 missing state-test transitions in `PollWorkerStateTests` and `DomainPollStateTests`:
  - Steady + consistent change → stays Steady (the happy-path continuation)
  - Steady + inconsistent change → falls back to Discovery (drift detection)
  - Steady + NoData → transitions to Suspended
  - Suspended + repeated NoData → stays Suspended
  - CycleTolerance boundary: 20.1% drift is inconsistent (edge case)
- Add actor-level tests in `PollWorkerPollingTests` for timing-critical Steady scenarios:
  - Steady timer fires at `LearnedCycle - SteadyBuffer` (30s early), not at DiscoveryInterval
  - 5 consecutive misses in Steady cause fallback to Discovery with correct interval
- Expose per-domain poll phase in gRPC `GetStatus` response:
  - Add `repeated DomainPollStatus domain_states` to the `ModelStatus` proto message
  - Map from `PollWorkerReport` domain states through the existing status pipeline
  - Add mapping + assertion tests

## Capabilities

### New Capabilities
- `learning-cycle-tests`: Specification for state-test and actor-test coverage of the per-domain learning cycle state machine.

### Modified Capabilities
- `grpc-mapping-layer`: GetStatus ModelStatus gains per-domain poll phase fields for E2E observability.

## Non-goals

- Changing the learning algorithm itself (tolerance, miss threshold, backoff curve).
- Adding E2E or soak test steps that exercise real learning transitions (that depends on the per-domain visibility this change provides, and can follow separately).
- Exposing per-domain phase in the ha-njord integration (not needed for observability).

## Impact

- **Tests**: `Njord.Polling.Tests` (DomainPollStateTests, PollWorkerStateTests, PollWorkerPollingTests), `Njord.Grpc.Tests` (StatusMappingTests)
- **Proto**: `protos/njord/njord.proto` -- additive fields on `ModelStatus` (wire-compatible)
- **Code**: `StatusMappingExtensions`, `PollWorkerMessageMappingExtensions`, `PollStateEntry`/`PollWorkerReport`
- **Budget**: No impact -- no polling changes, test-only + read-only API additions
