## Why

A cross-codebase review against the sibling FunkArr project found that several persistent
actors in `Njord.Pipeline` and `Njord.Enrichment` make domain decisions themselves instead
of delegating to their state record. This already violates this repo's own
`actor-state-pattern` spec ("each handler delegates business logic to state extension
methods and only performs actor-specific operations"), which FunkArr enforces consistently
via its `State.ProcessCommand(cmd) -> (newState, evt)` convention. Fixing it now keeps the
actor layer testable without ActorSystem/TestKit and prevents the drift from compounding as
more poll-state and history logic gets added.

## What Changes

- `ForecastHistoryActor.OnRecordSnapshot`: move the nearest-point lookup, per-parameter
  value extraction, and consensus-median computation (currently inline in the actor) into a
  new `ForecastHistoryState` extension method (e.g. `ProcessSnapshot(snapshot, location,
  parameters, now) -> (state, evt)`). The actor only calls this method, then `Persist`s the
  returned event.
- `SchedulerActor.OnHashResult`: move the "hash changed vs. miss" decision into
  `SchedulerState`/`ModelPollState` as a named method (e.g. `ApplyHashResult(key, hash, now,
  discoveryInterval) -> (state, decision)`) instead of comparing `LastHash` in the actor.
- `SchedulerActor.OnFetchFailed` (`RateLimited` case): move the rate-limit-floor rule
  (`NextPollUtc < now + RateLimitMinDelay`) into `ModelPollState` as a named method (e.g.
  `ApplyRateLimited(key, now, minDelay, discoveryInterval)`) instead of mutating and
  reassigning the poll state in the actor.
- `SchedulerActor`: add a single `_options.FindLocation(name)` helper (extension method on
  `NjordOptions` or a private helper) to replace the three independent
  `_options.Locations.FirstOrDefault(l => l.Name.Equals(...))` call sites in `CreateTarget`,
  `OnTriggerImmediatePoll`, and `RecordPollCompletion`.
- `ForecastHistoryActor` and `BudgetTrackerActor`: register `Command<DeleteMessagesFailure>`
  and `Command<DeleteSnapshotsFailure>` handlers that log a warning (matching the existing
  `SaveSnapshotFailure` pattern), so these outcomes are no longer silently dropped to dead
  letters.
- `MqttConnectionActor`: replace the hand-rolled `Math.Pow(2, ...)` backoff calculation with
  the same injected `Backoff.Create()` policy (Servus.Resilience) that `SchedulerActor`
  already uses, and surface the currently-swallowed post-connect `SubscribeAsync` failure as
  a logged warning (and, if a caller is waiting, a failure response) instead of only a log
  line.
- `Njord.Pipeline.Tests`: replace the six trivial, single-use stub classes identified as
  NSubstitute candidates during this review (`FakeProvider : IBudgetProvider` in
  `BudgetThrottleStageSpec.cs`; `RecordingLogger : ILogger` in `StreamSupervisionSpec.cs`;
  the four `AlwaysAllow*Gate` always-true/zero-delay stubs in `PipelineConnectionSpec.cs`,
  `PipelineActorShutdownSpec.cs`, and `SinkRefConnectionSpec.cs`) with
  `Substitute.For<T>()`, per the existing NSubstitute convention in AGENTS.md. The six
  stateful/recording fakes identified in the same sweep (`FakeConnection`,
  `RecordingTransport`, the local `FakeOpenMeteoClient`/`TimingOpenMeteoClient`, and the two
  gate fakes that record acquired elements) stay as hand-rolled fakes — they match the
  documented exception for stateful, purpose-built test helpers.
- `Njord.Architecture.Tests`: add a spec asserting that the foundation layers
  (`Njord.Weather`, `Njord.Sensors`, `Njord.Messages`, `Njord.Persistence`) have no
  dependency on any `Akka.*` namespace, closing the one gap found when comparing coverage
  against FunkArr's `ArchitectureSpec.cs` (which already enforces this for its own
  Messages/Persistence layers).

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `actor-state-pattern`: the existing "Actor is a thin shell" requirement is unchanged in
  text — `ForecastHistoryActor` and `SchedulerActor` are brought into compliance with it,
  no requirement wording changes. One new observable scenario is added: persistence
  delete-failures (`DeleteMessagesFailure`/`DeleteSnapshotsFailure`) are now logged as a
  warning instead of silently reaching dead letters.
- `architecture-zone-enforcement`: one new requirement is added — the foundation layers
  (`Njord.Weather`, `Njord.Sensors`, `Njord.Messages`, `Njord.Persistence`) SHALL NOT
  reference any `Akka.*` namespace, matching the coverage FunkArr already has for its own
  foundation layers.

## Impact

- `src/Njord.Enrichment/ForecastHistoryActor.cs`, `src/Njord.Enrichment/ForecastHistoryState.cs`
- `src/Njord.Pipeline/SchedulerActor.cs`, `src/Njord.Pipeline/SchedulerState.cs`,
  `src/Njord.Pipeline/ModelPollState.cs`
- `src/Njord.Pipeline/BudgetTrackerActor.cs` (failure-handler addition only)
- `src/Njord.Mqtt/MqttConnectionActor.cs` (backoff + failure handling only)
- `src/Njord.Architecture.Tests/` (new layering spec)
- Matching test projects: `src/Njord.Enrichment.Tests/`, `src/Njord.Pipeline.Tests/`,
  `src/Njord.Mqtt.Tests/`
- No persistence DTO changes, no wire-format changes, no public message contract changes.

## Non-goals

- No change to retry/backoff timing values, consensus math, or hashing algorithm — this is
  a pure internal restructuring of where existing logic lives.
- No change to `BudgetTrackerActor.OnRecordApiCall`, which already delegates correctly, other
  than adding the two missing failure handlers.
- No change to the actual backoff delay values/formula result for `MqttConnectionActor` —
  only the mechanism (hand-rolled math vs. the shared `Backoff.Create()` policy) changes;
  reconnect timing should stay the same or very close to it.
- No change to persistence DTO shapes or `Version` fields (extend-only rule stays satisfied
  because nothing is added to the DTOs).
- Not addressing the `Njord.Grpc`, `Njord.Mqtt` discovery, or `Njord.Compute` findings from
  the same review — those are tracked as separate changes
  (`grpc-thin-adapters`, `mqtt-discovery-component-builder`, `compute-domain-dedup`).

## API budget impact

None. This change touches only internal actor/state code paths; it does not add, remove, or
alter any Open-Meteo polling behavior, request frequency, or weighting.
