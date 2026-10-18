## 1. Untested units

- [x] 1.1 `GatewayBudgetGate` spec with `FakeTimeProvider` (per-minute refill, burst, daily limit, rejection reasons)
- [x] 1.2 `SensorGrpcService` spec (accepted, rejected, unknown kind, unavailable actor)
- [x] 1.3 `PipelineHealthCheck` and `MqttConnectionHealthCheck` specs (healthy, stale, disconnected, disabled)
- [x] 1.4 Extract and spec the persistence provider selection from `AkkaSetupContainer`; spec `NjordApplicationSetup`
- [x] 1.5 Specs for `AddDerived`, `AddTrends` and `AddNjordOptions`; extend `MqttNetPublisher` argument and failure-path specs

## 2. Real time out of tests

- [x] 2.1 Replace the `Task.Delay` calls in `ForecastReadModelRewireSpec`, `ForecastReadModelSpec`, `SinkRefConnectionSpec` and `ResilientJsonConfigurationSourceSpec` with bounded waits; run each affected project 20 times to confirm stability
- [x] 2.2 Replace the `DateTimeOffset.UtcNow` deadline loop in `ForecastReadModelSpec` and the wall-clock data in `EnrichmentKindSpec`
- [x] 2.3 Give `ConfigHarness` and `DisabledAnalyzers` a `TimeProvider` parameter instead of `TimeProvider.System`

## 3. Shared helpers and defaults

- [x] 3.1 Replace default-reliant `new XOptions()` with `AppSettingsDefaults` (the remaining constructions set every field they read explicitly)
- [x] 3.2 Replace duplicated fakes by shared ones (`RecordingLogger` in `Njord.Tests.Shared`, one `RecordingTransport` in `Njord.Mqtt.Tests`)
- [x] 3.3 Assert the config-migration log (`RecordingLogger`), and the publish-failure and gateway-rejection warnings (event stream probe)

## 4. Documentation drift

- [x] 4.1 Fix `AGENTS.md` (`AddFastRetryBackoff`, Persistence.Tests Verify statement, test counts) and rewrite the stale parts of `.claude/skills/njord-actor-spec/SKILL.md`
- [x] 4.2 Remove the `Njord.IntegrationTests` special case from `DisabledTestArchitectureSpec` unless the integration change has created that project

## 5. Verification

- [x] 5.1 Run `dotnet format whitespace`, build `Njord.slnx`, run every test project, `openspec validate --all --no-interactive`, `scripts/check-openspec-root.sh`
