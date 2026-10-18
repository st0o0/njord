## Context

Findings come from a read-only survey of njord and FunkArr tests. njord already exceeds FunkArr in architecture specs, deterministic time, timeouts and golden masters, so this change closes njord's own gaps rather than copying FunkArr. Counts are from `[Fact]`/`[Theory]` methods; the AGENTS.md totals count theory rows.

## Goals / Non-Goals

**Goals:** a spec for every behavioural unit, no real-time waits, no stale documentation.

**Non-Goals:** real infrastructure tests (`integration-tests-real-infrastructure`), rule enforcement by analyzers (`test-rules-enforcement`), mutation testing.

## Decisions

1. **Test through public seams.** `GatewayBudgetGate` is tested directly with a `FakeTimeProvider` (constructor takes a `TimeProvider`; if it does not, add the parameter, defaulting to `TimeProvider.System`). `SensorGrpcService` follows the other gRPC service specs (`TestServerCallContext`, probe-backed actor marker). Health checks are constructed with the health monitor and a fake time provider.
2. **Persistence selection without a database.** `AkkaSetupContainer` computes provider name and connection string before calling `WithSqlPersistence`; extract that computation into an internal pure method (`PersistenceSelection.From(PersistenceOptions, StorageOptions)`) and test it. The Akka wiring itself is covered by the integration change.
3. **Delay replacements.** Each of the seven `Task.Delay` sites is replaced case by case: wait for an observable condition with `AwaitAssert(…, TestTimeouts.AwaitAssertMax)` in TestKit specs, a `TaskCompletionSource` signalled by the fake in plain async specs; the `Task.Delay(Timeout.Infinite, ct)` in Mqtt fake transports stays because it models a hanging call and is cancellation-bound.
4. **Shared fakes by demand.** A fake moves to `Njord.Tests.Shared` only if two projects use it (`FakeConfigManager`, poll manager and budget tracker fake actors, a recording MQTT transport). Single-use fakes stay private.
5. **Default options in specs.** `AppSettingsDefaults.Options<T>(…)` replaces `new XOptions()` when the spec relies on the default values; specs that set every relevant property explicitly keep their construction. The task list names the projects (Core 42, Pipeline 15, Mqtt 8, Grpc 4, Indices 2 call sites at the time of the survey).
6. **Doc drift fixes.** `AddFastRetryBackoff` is removed from `AGENTS.md` unless a spec needs it (then implemented in `Njord.Tests.Shared`); the skill file is rewritten from the current spec layout; a doc-reference check is a manual task here and an automated spec in `test-rules-enforcement`.

## Risks / Trade-offs

- Replacing delays can expose real timing assumptions; each replacement is run repeatedly (loop of 20) before it is accepted.
- Extracting the persistence selection adds a tiny production seam; it is pure and covered by its own spec.

## Migration Plan

No operator impact. Order: untested units, delay and clock removals, default-options cleanup, shared fakes, documentation.
