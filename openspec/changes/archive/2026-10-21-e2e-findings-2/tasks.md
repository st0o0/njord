## 1. F9: persist and restore coordinates (TDD)

- [x] 1.1 Update `PollWorkerSpecBase` to use `Location("lucerne", 47.05, 8.31)` and write failing specs in `Njord.Polling.Tests`: started worker persists/uses coordinates, recovered worker (journal replay) keeps them, snapshot restore keeps them, restart with changed coordinates refreshes the target, restart with identical target persists nothing
- [x] 1.2 Write failing specs for Apply and the persistence mapping: round trip keeps coordinates; `PollingStarted` / `PersistedPollWorkerState` without coordinates falls back to 0/0 (Njord.Polling.Tests, plus a wire-format check in Njord.Persistence.Tests that old JSON without the members deserializes)
- [x] 1.3 Append `double? Latitude = null, double? Longitude = null` to `PollingStarted` and `PersistedPollWorkerState`
- [x] 1.4 Fill coordinates in `PollWorker.OnStartPolling` and `GetPersistenceState`; use `?? 0` fallbacks in `PollWorkerStateExtensions.Apply` (both the first-start and already-started branches, replacing `Target`) and `FromPersistence`
- [x] 1.5 Change the early return in `OnStartPolling` to also require equal coordinates, so a changed target persists a new `PollingStarted`
- [x] 1.6 Keep the existing unused-using cleanup in the 5 Njord.Polling files (do not revert); make sure `dotnet format` passes (`PollWorkerState.cs` now starts with a blank line)
- [x] 1.7 Run Polling, Persistence and Architecture tests via `dotnet run`, then `dotnet format Njord.slnx --verify-no-changes`

## 2. E2E plan update (docs only, `e2e/E2E-TEST-PLAN.md`)

- [x] 2.1 S4.4: replace the `forecast` attribute check with `weather.get_forecasts` (type hourly, `?return_response`) expecting a non-empty list; note daily is unsupported
- [x] 2.2 S7.3: `severity` present only on alerts in state on, in {yellow, orange, red}; S7.5: `pm2_5_alert`; alert count 21 (`*_alert` entities)
- [x] 2.3 S8.1/S8.2: oneof `weather` / `airQuality`; S8.5: `precipitation` may be omitted when 0; note Marine NotFound for inland locations
- [x] 2.4 S9: `events[]`, feature = oneof case (e.g. `fog`), severity at `<case>.alert.severity`, `type_name` gone, S9.4 checks a feature oneof case is set
- [x] 2.5 S10.1: `GetCatalog` on `njord.v2.WeatherService`; S10.3: poll state per location (target phase + cycle)
- [x] 2.6 S11.1: `server_monthly_usage` is a percentage; S12.1: `/healthz` plain text `Healthy`/`Degraded`; S13.4: increment = sum of call weights of resolved domains (11 for the e2e config)

## 3. Verify

- [x] 3.1 `openspec validate e2e-findings-2 --no-interactive` passes; F4 (Marine backoff) and F7 intentionally need no code change
