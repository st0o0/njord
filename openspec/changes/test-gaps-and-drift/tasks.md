## 1. Untested units

- [ ] 1.1 `GatewayBudgetGate` spec with `FakeTimeProvider` (per-minute refill, burst, daily limit, rejection reasons)
- [ ] 1.2 `SensorGrpcService` spec (accepted, rejected, unknown kind, unavailable actor, deadline)
- [ ] 1.3 `PipelineHealthCheck` and `MqttConnectionHealthCheck` specs (healthy, stale, disconnected, disabled)
- [ ] 1.4 Extract and spec the persistence provider selection from `AkkaSetupContainer`; spec `NjordApplicationSetup`
- [ ] 1.5 Specs for `AddDerived`, `AddTrends` and `AddNjordOptions`; extend `MqttNetPublisher` argument and failure-path specs

## 2. Real time out of tests

- [ ] 2.1 Replace the `Task.Delay` calls in `ForecastReadModelRewireSpec`, `ForecastReadModelSpec`, `SinkRefConnectionSpec` and `ResilientJsonConfigurationSourceSpec` with bounded waits; run each affected project 20 times to confirm stability
- [ ] 2.2 Replace the `DateTimeOffset.UtcNow` deadline loop in `ForecastReadModelSpec` and the wall-clock data in `EnrichmentKindSpec`
- [ ] 2.3 Give `ConfigHarness` and `DisabledAnalyzers` a `TimeProvider` parameter instead of `TimeProvider.System`

## 3. Shared helpers and defaults

- [ ] 3.1 Replace default-reliant `new XOptions()` in Core, Pipeline, Mqtt, Grpc and Indices specs with `AppSettingsDefaults`
- [ ] 3.2 Move fakes used by two or more projects into `Njord.Tests.Shared`
- [ ] 3.3 Add `RecordingLogger` assertions to the config-migration, publish-failure and gateway-rejection specs

## 4. Documentation drift

- [ ] 4.1 Fix `AGENTS.md` (`AddFastRetryBackoff`, Persistence.Tests Verify statement, test counts) and rewrite the stale parts of `.claude/skills/njord-actor-spec/SKILL.md`
- [ ] 4.2 Remove the `Njord.IntegrationTests` special case from `DisabledTestArchitectureSpec` unless the integration change has created that project

## 5. Verification

- [ ] 5.1 Run `dotnet format whitespace`, build `Njord.slnx`, run every test project, `openspec validate --all --no-interactive`, `scripts/check-openspec-root.sh`
