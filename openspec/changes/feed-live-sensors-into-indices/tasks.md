## 1. Failing specs first (TDD)

- [ ] 1.1 Add `src/Njord.Pipeline.Tests/ForecastPipelineSensorSpec.cs`, modeled on `ForecastPipelineSettingsSpec.cs`, with a test actor registered as `ISensorHub` that answers `QuerySensorSnapshot` per location (design D4)
- [ ] 1.2 Spec `A_live_indoor_temperature_of_the_forecast_location_reaches_the_indices`: the hub holds `IndoorTemperature = 24.5` for the forecast's location, and the emitted `IndexComputed` differs from a run where only the configured indoor temperature applies (assert the concrete night-ventilation value)
- [ ] 1.3 Spec `Sensor_readings_of_another_location_are_not_used`: the hub answers `SensorSnapshotNotAvailable` for the forecast's location, and the result equals the configured-value result
- [ ] 1.4 Spec `A_forecast_is_analyzed_when_no_sensor_hub_is_registered`: all pipeline events of the forecast are emitted
- [ ] 1.5 Spec `A_forecast_is_analyzed_when_the_sensor_hub_does_not_reply`: the hub never replies, and all events are emitted within the lookup timeout plus a bounded margin
- [ ] 1.6 Add `src/Njord.Grpc.Tests/SensorProtoMappingsSpec.cs`, spec `A_reading_is_forwarded_under_the_configured_location_spelling`: request location `"luzern"` with the catalog location `"Luzern"` gives an `UpdateReading` whose reading has location `"Luzern"`
- [ ] 1.7 Run both suites and confirm that 1.2 and 1.6 fail on the current code (1.3–1.5 may already pass; they guard the fallback)

Conventions: sealed `Spec` classes, BDD names, `[Fact(Timeout = TestTimeouts.Hosted)]` for actor specs and plain `[Fact]` for the pure mapping spec, real-time bounded waits that fail with a descriptive message (do not pass `TestTimeouts.AwaitAssertMax` to a dilated TestKit wait whose dilated value exceeds `TestTimeouts.Hosted`), `TestContext.Current.CancellationToken` on every await, no `Thread.Sleep`.

## 2. Implementation

- [ ] 2.1 `src/Njord.Pipeline/ForecastPipeline.cs`: remove the sensor `Source.ActorRef` side channel, `_sensorRef`, the `Receive<SensorSnapshot>` handler and the empty-snapshot convention; zip each forecast with the settings channel only
- [ ] 2.2 `src/Njord.Pipeline/ForecastPipeline.cs`: add a `SelectAsync(1, ...)` stage that looks up `ISensorHub` from the actor system's `ActorRegistry` per forecast, asks `QuerySensorSnapshot(new LocationId(forecast.Location))` with a named 2 s timeout, and yields the snapshot or `null` (not registered, `SensorSnapshotNotAvailable`, timeout, exception; log Debug/Warning as in design D2); pass it to `IndexAnalyzer.Compute`
- [ ] 2.3 `src/Njord.Grpc/Mapping/SensorProtoMappings.cs`: forward the matched catalog location's `Id.Name` instead of `request.Location`
- [ ] 2.4 Specs 1.2–1.6 and all existing Pipeline and Grpc specs pass; run the Pipeline project 3 times to check for flakiness

## 3. Documentation

- [ ] 3.1 `AGENTS.md`: in the architecture guardrails, replace "Sensor data enters via a `Source.ActorRef` + `ReuseLatest` side channel" with the per-forecast SensorHub lookup, and keep the "SensorHub for external sensor input" bullet consistent with it

## 4. Validation

- [ ] 4.1 From `src/`: `dotnet build Njord.slnx` gives 0 warnings and 0 errors
- [ ] 4.2 From `src/`: `dotnet run --project Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj --no-build` passes
- [ ] 4.3 From `src/`: `dotnet run --project Njord.Grpc.Tests/Njord.Grpc.Tests.csproj --no-build`, `dotnet run --project Njord.SensorHub.Tests/Njord.SensorHub.Tests.csproj --no-build` and `dotnet run --project Njord.Architecture.Tests/Njord.Architecture.Tests.csproj --no-build` pass
- [ ] 4.4 From `src/`: run the full loop (`for p in Njord.*Tests; do [ "$p" = Njord.Tests.Shared ] && continue; dotnet run --project "$p/$p.csproj" --no-build; done`); all tests pass. Update "Current total" in `AGENTS.md`
- [ ] 4.5 `git diff --stat` shows no `*.verified.*` change
- [ ] 4.6 From the repo root: `openspec validate --all --no-interactive` passes
