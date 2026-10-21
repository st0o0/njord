## Why

The latest e2e run exposed a real bug and a set of stale expectations in the e2e plan. After a PollWorker is persisted and recovered, its target location has coordinates `(0, 0)`; logs show `Fetch failed for Lucerne (0, 0)/Marine ...`. The e2e plan also still describes behaviour that no longer exists (attribute names, proto field names, oneof shapes), so a run reports false failures.

## What Changes

- **F9 fix**: `PollingStarted` and `PersistedPollWorkerState` gain nullable, defaulted `Latitude`/`Longitude` (appended last, extend-only). `PollWorker` fills them when persisting; `PollWorkerStateExtensions.Apply` and `PollWorkerPersistenceMappingExtensions.FromPersistence` use them and fall back to `0/0` when null (old journals and snapshots).
- `PollWorker.OnStartPolling` no longer returns early when the worker is already started but the incoming target has different coordinates; it persists a new `PollingStarted` so the target is refreshed.
- Tests use `Location("lucerne", 47.05, 8.31)` instead of `(0, 0)`, which masked the bug.
- **E2E plan** (`e2e/E2E-TEST-PLAN.md`, docs only): update S4.4, S7.3, S7.5, S8.1/S8.2/S8.5, S9, S10.1/S10.3, S11.1, S12.1, S13.4 and the alert count to match the real behaviour.
- **F4 verified, no change**: the Marine failure backoff behaves correctly (inland locations fail and back off as designed). Follow-up idea, out of scope: a `NoCoverage` outcome reason so inland Marine is not reported as a plain failure.

Non-goals: F7 (ClusterSingletonProxy startup warnings); `OpsGrpcService.TriggerPoll` building `Location(name, 0, 0)` is harmless because the gateway matches locations by name (`_options.FindLocation`), so it stays. Data was never affected by F9 for the same reason.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `persistence-records`: extend-only coordinates on the poll worker event and snapshot, with fallback for old data.
- `e2e-test-plan`: the plan's expectations track the real service behaviour.

## Impact

- `src/Njord.Persistence/Events/Polling/PollingStarted.cs`, `PersistedPollWorkerState.cs`
- `src/Njord.Polling/PollWorker.cs`, `PollWorkerStateExtensions.cs`, `PollWorkerPersistenceMappingExtensions.cs`
- `src/Njord.Tests.Shared` (`PollWorkerSpecBase`), `Njord.Polling.Tests`, `Njord.Persistence.Tests`
- `e2e/E2E-TEST-PLAN.md`
- No wire/proto change; persistence is extend-only (0.x), old journals still replay.
