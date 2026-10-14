## 1. Failing specs first

- [x] 1.1 `src/Njord.Core.Tests/StreamSupervisionSpec.cs`: wrapped `TimeoutException` (one and two levels) resumes and logs the inner type, wrapped `InvalidOperationException` stops, multi-cause aggregate stops; plus a `SelectAsyncUnordered` stream spec with an already-faulted `TimeoutException` task that keeps flowing
- [x] 1.2 `src/Njord.Pipeline.Tests/PollCycleTrackerSpec.cs` (new, plain class): failed outcome completes the cycle with correct counts and duration, consecutive cycles stay separate, retry replaces the failed outcome, unknown models/locations open no cycle, `OpenCycles` is 0 after completion
- [x] 1.3 `src/Njord.Pipeline.Tests/PollSchedulerCycleSpec.cs` (new, TestKit, `MeterListener` filtered by a unique location): a cycle with one failed model records `njord_poll_cycle_models` 2 and `njord_poll_cycle_failed_models` 1; two rounds with a failure complete two cycles of 3 models
- [x] 1.4 `src/Njord.OpenMeteo.Tests/OpenMeteoGatewaySpec.cs` (new, TestKit): a throwing fetch yields `FetchForecastFailed(Transport)` and the next fetch completes; a stopped fetch stream (throttle stage fault) fails the in-flight request, the next fetch completes and a SourceRef taken before still receives forecasts
- [x] 1.5 Run the new specs and record the failure messages

## 2. Stream supervision (Njord.Core)

- [x] 2.1 `src/Njord.Core/Actors/StreamSupervision.cs`: unwrap single-inner `AggregateException` before `Classify` and log the unwrapped type

## 3. Poll cycle completion (Njord.Pipeline)

- [x] 3.1 `src/Njord.Pipeline/PollCycleTracker.cs`: internal tracker with per-model outcomes, `PollCycleSummary`, `OpenCycles`
- [x] 3.2 `src/Njord.Pipeline/Telemetry.cs`: add `njord_poll_cycle_failed_models` gauge; extend `src/Njord.Pipeline.Tests/TelemetrySpec.cs`
- [x] 3.3 `src/Njord.Pipeline/PollScheduler.cs`: feed the tracker from completed and failed fetches, record metrics and the new summary log from `PollCycleSummary`

## 4. Gateway resilience (Njord.OpenMeteo)

- [x] 4.1 `src/Njord.OpenMeteo/OpenMeteoGateway.cs`: catch exceptions from `FetchAsync` into a `Transport` failure with a Warning log
- [x] 4.2 `src/Njord.OpenMeteo/OpenMeteoGateway.cs`: generation-tagged `StreamCompleted`; on the current generation fail pending requests and re-materialize only the fetch stream
- [x] 4.3 `src/Njord.Tests.Shared/FakeOpenMeteoClient.cs`: `ThrowingModels` toggle used by the gateway spec

## 5. Validation

- [x] 5.1 `dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj`, `dotnet run --project Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj`, `dotnet run --project Njord.OpenMeteo.Tests/Njord.OpenMeteo.Tests.csproj`, each three times (from `src/`)
- [x] 5.2 `dotnet build Njord.slnx` with 0 warnings and 0 errors, then the full per-project loop from `AGENTS.md`; update "Current total" in `AGENTS.md`
- [x] 5.3 `openspec validate complete-failed-poll-cycles --no-interactive`
