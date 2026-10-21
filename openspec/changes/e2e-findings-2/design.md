## Context

Root cause of F9, verified in code: `PollingStarted(string Location, int[] Domains, TimeSpan DiscoveryInterval, DateTimeOffset Utc)` and `PersistedPollWorkerState` carry only the location name. `PollWorker.OnStartPolling` persists `cmd.Target.Location.Name`; `PollWorkerStateExtensions.Apply` rebuilds `new Location(evt.Location, 0, 0)`; `FromPersistence` does the same on snapshot restore. The early return in `OnStartPolling` (already active, same discovery interval) means a started worker never refreshes its target. Data fetches work only because `OpenMeteoGateway` resolves the location by name. `PollWorkerSpecBase` uses `Location("lucerne", 0, 0)`, which hides the defect.

## Goals / Non-Goals

**Goals:** persist and restore coordinates; refresh the target when coordinates change; keep old journals and snapshots readable.

**Non-Goals:** changing `OpsGrpcService.TriggerPoll` (`Location(name, 0, 0)`, matched by name); F7 proxy warnings; a `NoCoverage` reason for Marine (F4 follow-up idea).

## Decisions

1. **Extend-only nullable members.** Append `double? Latitude = null, double? Longitude = null` to `PollingStarted` and `PersistedPollWorkerState`, per the persistence rule (new members nullable or defaulted, 0.x). No new event type.
2. **Fill on persist.** `OnStartPolling` passes the command target's latitude and longitude into `PollingStarted`; `GetPersistenceState` writes the target location's coordinates.
3. **Fallback on read.** `Apply` and `FromPersistence` use `Latitude ?? 0` / `Longitude ?? 0`. Old data behaves as before until the next `StartPolling` refreshes it.
4. **Refresh instead of early return.** The early-return condition additionally requires equal coordinates. If they differ, a new `PollingStarted` is persisted; `Apply` must replace `Target` in its already-started branch so the new coordinates take effect. Domain states and timers are kept; `ScheduleAllDomains` and `Report(Planned)` run as for an interval change.
5. **Mapping stays in `PollWorkerPersistenceMappingExtensions`** (state <-> record); the actor only builds the event.

## Risks / Trade-offs

- Old journals replay with `0/0` until the PollManager re-sends `StartPolling` at startup, which refreshes the target via decision 4. No migration code.
- Exact double equality: coordinates come from config verbatim, so exact comparison is intended.

## Test plan

Behavioural specs only, using `Location("lucerne", 47.05, 8.31)`; xUnit v3, `Spec` suffix, `NjordActorSpec`, `[Fact(Timeout = ...)]`, `TestContext.Current.CancellationToken`, `FakeTimeProvider`. Cover: started worker reports and uses coordinates; recovered worker (journal replay) keeps them; snapshot restore keeps them; Apply and mapping round trip; old event without coordinates falls back to 0/0; restart with changed coordinates refreshes the target. Update `PollWorkerSpecBase` to non-zero coordinates.
