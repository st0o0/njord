## 1. New messages

- [x] 1.1 Added `QueryPollHealth` / `PollHealthResult(DateTimeOffset StartedUtc, DateTimeOffset? LastSuccessfulPollUtc)` to `src/Njord.Messages/Pipeline/QueryPollHealth.cs`
- [x] 1.2 Added `QueryMqttConnectionStatus` / `MqttConnectionStatusResult(bool IsConnected, DateTimeOffset? DisconnectedSince)` to `src/Njord.Mqtt/Health/QueryMqttConnectionStatus.cs` (MQTT boundary rule: types with "Mqtt" in name must live in Njord.Mqtt)

## 2. Actor handlers

- [x] 2.1 PollManager: tracks `_startedUtc` (constructor) and `_lastSuccessfulPollUtc` (on Changed/Unchanged). Handles `QueryPollHealth` returning `PollHealthResult`. Removed PollHealthState dependency.
- [x] 2.2 BudgetTracker: already handles `QueryBudgetStatus` returning `BudgetStatusResult` with all needed fields. Removed BudgetUsageState dependency.
- [x] 2.3 MqttConnection: handles `QueryMqttConnectionStatus` returning `MqttConnectionStatusResult` from `_isConnected` and `_disconnectedSince` fields. Tracks connection state on the actor thread.

## 3. Rewrite health checks

- [x] 3.1 PipelineHealthCheck: injects `ActorRegistry`, resolves `IPollManager`, Ask with 3s timeout. `TryGet` → Unhealthy, `AskTimeoutException` → Unhealthy. Evaluate takes `PollHealthResult`.
- [x] 3.2 BudgetHealthCheck: injects `ActorRegistry`, resolves `IBudgetTracker`, Ask `QueryBudgetStatus`. Evaluate takes `BudgetStatusResult` directly (no separate BudgetUsage/BudgetAllowance).
- [x] 3.3 MqttConnectionHealthCheck: injects `ActorRegistry`, resolves `IMqttConnectionActor`, Ask `QueryMqttConnectionStatus`.

## 4. Delete state holders

- [x] 4.1 Deleted `PollHealthState` class and `PollHealth` record (was in same file)
- [x] 4.2 Deleted `BudgetUsageState` class and `BudgetUsage` record (was in same file)
- [x] 4.3 Replaced `MqttConnectionState` with `MqttSessionCounter` (session counter for publisher dedup only). MqttConnection tracks `_isConnected`/`_disconnectedSince` on actor thread.
- [x] 4.4 Removed state holder registrations: `PollHealthState` from PipelineSetupContainer, `BudgetUsageState` from BudgetSetupContainer, `MqttConnectionState` → `MqttSessionCounter` in MqttSetupContainer
- [x] 4.5 Removed all Volatile.Write calls from actors (PollManager: `_health.RecordSuccessfulPoll` → `_lastSuccessfulPollUtc = now`, BudgetTracker: `_usage.Update` removed, MqttConnection: `_healthState.Connected/Disconnected` → `_isConnected`/`_disconnectedSince`/`_sessionCounter.Increment`)

## 5. BudgetGaugeRegistration

- [x] 5.1 Changed from `IHostedService` to `BackgroundService`. Periodically Asks BudgetTracker every 5s to refresh gauge values (stored in volatile fields read by observable gauge lambdas). Simplified test to not depend on BudgetUsageState.

## 6. Update tests

- [x] 6.1 PipelineHealthCheckSpec: uses `PollHealthResult` instead of `PollHealth`. Removed state test.
- [x] 6.2 BudgetHealthCheckSpec: uses `BudgetStatusResult` directly. Removed state holder test and BudgetUsage references.
- [x] 6.3 MqttConnectionHealthCheckSpec: uses `MqttConnectionStatusResult`. Removed state swap test.
- [x] 6.4 PollManagerSpec: uses `QueryPollHealth`/Ask to verify health updates instead of reading state holder.
- [x] 6.5 MqttConnectionLifecycleSpec: uses `QueryMqttConnectionStatus`/Ask to verify connection state. All 6 MQTT test files updated from `MqttConnectionState` to `MqttSessionCounter`.
- [x] 6.6 BudgetGaugeRegistrationSpec: simplified, removed BudgetUsageState integration test.
- [x] HealthSetupContainerSpec: removed singleton tests for deleted types, updated MqttConnectionState → MqttSessionCounter test.
- [x] BudgetTrackerSpec: removed `_usage` field and state holder assertions.

## 7. Validation

- [x] 7.1 `dotnet build Njord.slnx` — 0 errors, 0 warnings
- [x] 7.2 Pipeline.Tests: 160 pass, Budget.Tests: 34 pass, Mqtt.Tests: 170 pass, Njord.Tests: 182 pass
- [x] 7.3 Architecture.Tests: 122 pass
- [x] 7.4 `dotnet format Njord.slnx --verify-no-changes` — clean
