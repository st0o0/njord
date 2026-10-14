## 1. Contracts

- [x] 1.1 Add `FetchForecastRejected(LocationId Location, string ModelId, FetchRejectionReason Reason)` and the `FetchRejectionReason` enum (`QueueFull`, `Budget`, `PipelineUnavailable`, `Expired`) to `src/Njord.Messages/Forecast/` (one file each)
- [x] 1.2 Add `SensorSnapshots(ImmutableDictionary<LocationId, SensorSnapshot> Snapshots)` to `src/Njord.Messages/Sensors/SensorSnapshots.cs`
- [x] 1.3 Replace the `RequestForecastStream*` records with `RequestForecastSink(long RequestId)`, `RequestForecastSinkCompleted(RequestId, ISinkRef<Forecast>)` and `RequestForecastSinkFailed(RequestId, Exception)` in `src/Njord.Core/Actors/StreamWiringMessages.cs`
- [x] 1.4 Add `FetchTimeouts(Admission, Http, Reply)` with `Default` (20 s / 30 s / 60 s) to `src/Njord.Core/Configuration/Polling/FetchTimeouts.cs`; register it with `TryAddSingleton` in `OpenMeteoSetupContainer` and `PipelineSetupContainer`, use `Http` for the typed client; spec `src/Njord.Core.Tests/.../FetchTimeoutsSpec.cs` pins `Reply > Admission + Http`

## 2. Pipeline input hub (TDD)

- [x] 2.1 Shared test helper `src/Njord.Tests.Shared/PipelineProducer.cs`: asks `RequestForecastSink` and returns a queue running into the SinkRef
- [x] 2.2 Write failing specs `src/Njord.Pipeline.Tests/ForecastPipelineInputSpec.cs` (sealed, `[Fact(Timeout = TestTimeouts.Hosted)]`): output available before any producer; two producers both analyzed; a producer that completes or fails does not end the hub and the consumer SourceRef stays alive; trends get `previous` across a producer change; a stale-id-free handshake reply carries the request id
- [x] 2.3 Rewrite `ForecastPipeline` (`src/Njord.Pipeline/ForecastPipeline.cs`): `MaterializeAnalysisPipeline` in `PreStart` (MergeHub → ZipWith(sensors, settings) → StatefulSelectMany → BroadcastHub), `RequestForecastSink` handler with `RecoverWithRetries`, graph end → throw; remove gateway resolution, stash, rewire backoff, `SensorLookupTimeout` and the per-forecast sensor ask
- [x] 2.4 Wrap `ForecastPipeline` in BackoffOnFailure in `PipelineSetupContainer`; remove `njord_pipeline_stream_rewires_total` from `src/Njord.Pipeline/Telemetry.cs` and `TelemetrySpec`
- [x] 2.5 Port `ForecastPipelineSettingsSpec`, `ForecastPipelineConsensusToggleSpec` and `PipelineConnectionSpec` to the SinkRef producer helper; delete `ForecastPipelineRewireSpec` (gateway rewire behavior removed)

## 3. Sensor snapshot push (TDD)

- [x] 3.1 Rewrite `src/Njord.Pipeline.Tests/ForecastPipelineSensorSpec.cs` for pushed maps: live value of the forecast's location used, other location not used, no push → configured values, case-insensitive key; see them fail
- [x] 3.2 Pipeline: `SensorSnapshots` handler feeds the sensor side channel (initial empty map, `RepeatPrevious`); stage looks up the forecast's location
- [x] 3.3 Failing specs in `src/Njord.SensorHub.Tests/SensorHubSpec.cs`: accepted reading pushes the map, map covers all fresh locations, stale reading excluded, rejected reading pushes nothing, no pipeline registered keeps running; plus `SensorHubStateSpec` for the map builder
- [x] 3.4 `SensorHub` (`src/Njord.SensorHub/SensorHub.cs`) pushes on accepted readings, every expiry tick and once when `GetActorAsync<IForecastPipeline>` resolves; `SensorHubState.GetSnapshots` builds the map

## 4. Gateway request/response (TDD)

- [x] 4.1 Add a call counter to `src/Njord.Tests.Shared/FakeOpenMeteoClient.cs`
- [x] 4.2 Rewrite `src/Njord.OpenMeteo.Tests/OpenMeteoGatewaySpec.cs` with a fake pipeline vending SinkRefs: throwing fetch, next fetch after throw, burst → each answered once with `Rejected(QueueFull)` for the overflow, `Rejected(Budget)` when exhausted (one `RecordApiCall`), failed stream answers open requests and the restarted gateway fetches again, six requests around a stream stop answered once, requested spelling, configured names reach the pipeline, two requests for one target
- [x] 4.3 New `src/Njord.OpenMeteo.Tests/OpenMeteoGatewayAttachSpec.cs`: `Rejected(PipelineUnavailable)` without fetching before attach; re-attach after pipeline termination; re-attach counted in `njord_openmeteo_pipeline_reattaches_total`
- [x] 4.4 Rewrite `OpenMeteoGateway` (`src/Njord.OpenMeteo/OpenMeteoGateway.cs`): DropNew queue with per-request `PipeTo` offer outcome, stream end → answer open requests and throw, SinkRef handshake with timer and backoff, forecast branch, replies only after the branch offer
- [x] 4.5 `GatewayThrottleStage` emits admissions/rejections against the admission deadline (`src/Njord.OpenMeteo/GatewayBudgetGate.cs`); add the re-attach counter to `src/Njord.OpenMeteo/Telemetry.cs` and its `TelemetrySpec`
- [x] 4.6 Cross-actor spec `src/Njord.Pipeline.Tests/GatewayRestartSpec.cs`: real gateway + real pipeline; killing the gateway keeps the consumer SourceRef alive and trends still receive `previous`

## 5. Scheduler Ask (TDD)

- [x] 5.1 Failing specs `src/Njord.Pipeline.Tests/PollSchedulerFetchReplySpec.cs`: silent gateway → reply timeout → transient backoff; `Rejected(Budget)` → ≥ 5 min; `Rejected(QueueFull)` → 1 min; rejection counts as failed cycle outcome
- [x] 5.2 `PollScheduler`: Ask with `FetchTimeouts.Reply` + `PipeTo`, handle `FetchForecastRejected`, drop the `Ack` replies; adapt the scheduler specs that synchronized on `Ack` to query the poll states instead and register `FetchTimeouts`

## 6. Docs and validation

- [ ] 6.1 Rewrite the data-flow guardrail in `AGENTS.md`; update the test totals
- [ ] 6.2 From `src/`: `dotnet build Njord.slnx` (0 warnings), then `dotnet run --project Njord.OpenMeteo.Tests/Njord.OpenMeteo.Tests.csproj`, `dotnet run --project Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj`, `dotnet run --project Njord.SensorHub.Tests/Njord.SensorHub.Tests.csproj` three times each, `dotnet run --project Njord.Architecture.Tests/Njord.Architecture.Tests.csproj`, then the full test loop
- [ ] 6.3 Manual run `cd src/Njord && timeout -s INT 60 dotnet run` with `src/Njord/data` backed up and restored: forecasts fetched and analyzed, no njord dead letters or unhandled messages
- [ ] 6.4 `openspec validate --all --no-interactive`
