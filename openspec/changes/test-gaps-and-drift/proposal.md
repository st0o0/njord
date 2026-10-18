## Why

A survey of the test setup (compared with FunkArr) found production units without any spec, real-clock leftovers in tests, and documentation that no longer matches the code. The concrete token-bucket `GatewayBudgetGate` (231 lines) has no spec although an OpenSpec requirement describes a refill test; `SensorGrpcService` is the only one of four gRPC services without a spec; both health checks, `NjordApplicationSetup`, the SQL persistence wiring of `AkkaSetupContainer` and the `AddDerived`/`AddTrends` registrations are untested. Tests still use `Task.Delay` (7 places), a `DateTimeOffset.UtcNow` deadline loop (`ForecastReadModelSpec`) and wall-clock test data (`EnrichmentKindSpec`), which `deterministic-test-infrastructure` forbids. `AGENTS.md` names a non-existent `AddFastRetryBackoff()` and claims Persistence.Tests uses no Verify; the `njord-actor-spec` skill lists several deleted spec files. About 70 specs build `new XOptions()` instead of using `AppSettingsDefaults`.

## What Changes

- Add specs for every untested unit above: `GatewayBudgetGate` (refill, burst, per-minute and daily limits, rejection reasons, using `FakeTimeProvider`), `SensorGrpcService` (accepted, rejected, unknown sensor, unavailable actor), `PipelineHealthCheck`, `MqttConnectionHealthCheck`, `NjordApplicationSetup`, the SQL persistence branch selection of `AkkaSetupContainer` (provider and connection string, without a database), `AddDerived`/`AddTrends`/`AddNjordOptions`, `MqttNetPublisher` argument handling beyond the three static cases.
- Remove real-time dependencies from tests: replace `Task.Delay` with `AwaitAssert`/`AwaitCondition` or a fake time advance, replace the UtcNow deadline loop with a bounded wait, use `TestTimeConstants` in `EnrichmentKindSpec`, and let `ConfigHarness`/`DisabledAnalyzers` take a `TimeProvider`.
- Replace default-reliant `new XOptions()` in specs with `AppSettingsDefaults` where the default values matter.
- Share duplicated hand-written fakes (transports, config manager, poll manager and budget tracker fake actors) through `Njord.Tests.Shared` where two or more projects use them.
- Fix documentation drift: remove or implement `AddFastRetryBackoff`, correct the Persistence.Tests Verify statement and the test counts in `AGENTS.md`, rewrite the stale parts of `.claude/skills/njord-actor-spec/SKILL.md`, and drop the `Njord.IntegrationTests` special case from `DisabledTestArchitectureSpec` if no such project exists after `integration-tests-real-infrastructure`.
- Add assertions for logging where behaviour depends on it (`RecordingLogger` for warnings in config migration, publish failure and gateway rejection paths).

## Capabilities

### New Capabilities
<!-- none -->

### Modified Capabilities
- `test-coverage-gaps`: the previously untested units get required specs.

## Impact

- Test projects Grpc, OpenMeteo, Mqtt, Pipeline, Core, host and `Njord.Tests.Shared`; small production seams only if a unit cannot be tested otherwise (a `TimeProvider` parameter, an `internal` method with `InternalsVisibleTo`); `AGENTS.md`, the skill file, `DisabledTestArchitectureSpec`.
- No production behaviour change. Not breaking; commit type `test:` (plus `docs:` for drift fixes).
- Applies after the structure changes (`snapshot-naming`, `analysis-contracts-per-feature`, `feature-owned-options`) so specs are written once against final names.
