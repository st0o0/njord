## Context

`ForecastHistoryActor` and `SchedulerActor` currently make fachliche decisions inline in
their `Command<>` handlers (hash-comparison, rate-limit floor, consensus reduction) instead
of via the state record, per `openspec/specs/actor-state-pattern/spec.md`. The existing
pattern (`Apply`/`ApplyRecover`/`GetSnapshot`/`GetPersistenceState`/`FromPersistence`) is
already established and used correctly by `BudgetTrackerActor`; this change extends the same
naming convention to the two actors that drifted from it.

## Goals / Non-Goals

**Goals:**
- Every fachliche decision in `SchedulerActor` and `ForecastHistoryActor` lives in a named
  state-record method, callable and testable without an `ActorSystem`.
- No behavior change: same events persisted, same retry timing, same consensus values.
- Close the `DeleteMessagesFailure`/`DeleteSnapshotsFailure` logging gap on the two affected
  persistent actors.

**Non-Goals:**
- Not redesigning the `Become`/`Unbecome` state machine in `SchedulerActor`.
- Not changing `ModelPollState`'s persisted DTO shape or `BudgetTrackerActor`'s already
  spec-compliant `OnRecordApiCall`.
- Not touching `MqttConnectionActor` (separate, unrelated finding about hand-rolled backoff).

## Decisions

1. **`ForecastHistoryState.ProcessSnapshot(snapshot, location, parameters, now) ->
   (ForecastHistoryState, ForecastRecord)`** — moves the nearest-point lookup, per-parameter
   value extraction, and `ConsensusComputer.ComputeMedian` call out of
   `ForecastHistoryActor.OnRecordSnapshot`. The actor becomes:
   ```csharp
   private void OnRecordSnapshot(RecordSnapshot msg)
   {
       var (updated, evt) = _state.ProcessSnapshot(msg.Snapshot, _location, _parameters, _timeProvider.GetUtcNow());
       var dto = ForecastHistoryDtoMapping.ToDto(evt);
       Persist(dto, _ =>
       {
           _state = updated;
           if (_state.EventsSinceSnapshot >= _state.SnapshotInterval) { SaveSnapshot(_state.GetPersistenceState()); _state = _state.ResetSnapshotCounter(); }
       });
   }
   ```
   Alternative considered: keep the computation in a static helper in `Njord.Compute` and
   call it from the actor. Rejected — the existing convention in this codebase is that
   state records own command-to-event derivation (`Apply`/`GetSnapshot` live on the state
   record, not on a separate static service), and `ForecastHistoryState` already composes
   `Njord.Compute.Analysis.ConsensusComputer`, so the method fits naturally there.

2. **`ModelPollState`/`SchedulerState` get two new methods**:
   - `ApplyHashResult(key, hash, now, discoveryInterval) -> (SchedulerState, HashDecision)`
     where `HashDecision` is a small internal enum/record (`Changed` | `Miss`) replacing the
     actor's `if (currentPollState.LastHash != result.Hash)` branch.
   - `ApplyRateLimited(key, now, minDelay, discoveryInterval) -> SchedulerState`, folding the
     existing `ApplyTransientFailure` call plus the `NextPollUtc` floor adjustment into one
     state-owned step.
   The actor's `OnHashResult`/`OnFetchFailed` keep their logging, metrics, and
   `ScheduleNext`/`Ack` calls — only the state decision moves.

3. **Location lookup**: add `NjordOptionsExtensions.FindLocation(this NjordOptions options,
   string name)` in `Njord.Core/Configuration` (same project that already hosts
   `NjordOptionsValidator`), replacing the three duplicated `FirstOrDefault` call sites.

4. **Delete-failure handlers**: add `Command<DeleteMessagesFailure>`/
   `Command<DeleteSnapshotsFailure>` to `BudgetTrackerActor` and `ForecastHistoryActor`,
   mirroring the existing `SaveSnapshotFailure` warning-log pattern exactly (same log level,
   same message shape with `{PersistenceId}`).

5. **`MqttConnectionActor` backoff**: replace the hand-rolled `Math.Pow(2, Math.Min(_connectAttempts
   - 1, 6))` with the injected `Backoff.Create()` policy from Servus.Resilience, matching
   how `SchedulerActor` already computes its retry delay. Also add a failure path for the
   post-connect `SubscribeAsync` call: today a thrown/failed subscribe is caught and only
   logged; it should additionally surface as a typed failure (e.g. reuse or add a
   `SubscriptionFailed` message) so a waiting caller/supervisor can react instead of the
   connection silently sitting in a half-working state.

6. **NSubstitute cleanup in `Njord.Pipeline.Tests`**: replace the six trivial, single-use
   stubs (`FakeProvider`, `RecordingLogger`, and the four `AlwaysAllow*Gate` variants) with
   `Substitute.For<IBudgetProvider>()`, `Substitute.For<ILogger>()`, and
   `Substitute.For<IBudgetGate<T>>()` respectively, each returning a fixed value via
   `.Returns(...)`. This is the first use of NSubstitute in the solution, so it also adds
   the `NSubstitute` package reference to `Njord.Pipeline.Tests.csproj` via `dotnet add
   package` (central package management — version goes in `Directory.Packages.props`).
   The six stateful/recording fakes found in the same sweep are explicitly kept as-is.

7. **Architecture test for Akka-free foundation layers**: add one spec (new `[Fact]` in
   `LayerReferenceSpec.cs` or `ZoneArchitectureSpec.cs`, whichever already asserts
   per-library reference sets) checking that `Njord.Weather`, `Njord.Sensors`,
   `Njord.Messages`, and `Njord.Persistence` have no `Akka.*` dependency, mirroring
   FunkArr's `ArchitectureSpec.cs` framework-dependency check.

## Risks / Trade-offs

- [Risk] Renaming the decision path changes the exact sequence in which `Persist` callbacks
  run relative to logging/metrics. → Mitigation: keep all `Persist`-callback side effects
  (health updates, telemetry, `ScheduleNext`, `Sender.Tell`) exactly where they are in the
  actor; only the pure decision/computation step moves into the state method.
- [Risk] `ProcessSnapshot` needs `_parameters` and `_location`, which are currently actor
  constructor fields, not state fields — passing them as method parameters (not storing them
  on `ForecastHistoryState`) keeps the state record's persisted shape unchanged. → No
  mitigation needed, this is the chosen design (parameters stay actor-owned config, not
  persisted state).
- [Risk] Switching `MqttConnectionActor`'s backoff mechanism could change the exact
  reconnect delay sequence. → Mitigation: configure `Backoff.Create()` with the same
  min/max/multiplier that the current `Math.Pow` formula produces for the first ~6 attempts,
  and add a test asserting the delay sequence is unchanged (or document the new sequence if
  intentionally adjusted).

## Migration Plan

Pure internal refactor, single deploy, no data migration. Rollback is a plain revert since
no persistence DTO or wire message changes.

## Open Questions

None.
