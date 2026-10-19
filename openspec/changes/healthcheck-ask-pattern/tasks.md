## 1. New messages

- [ ] 1.1 Add `QueryPollHealth` / `PollHealthResult(DateTimeOffset StartedUtc, DateTimeOffset? LastSuccessfulPollUtc)` to `src/Njord.Messages/Pipeline/`
- [ ] 1.2 Add `QueryMqttConnectionStatus` / `MqttConnectionStatusResult(bool IsConnected, DateTimeOffset? DisconnectedSince)` to `src/Njord.Messages/` (check MQTT boundary rule — may need to go in Njord.Mqtt if messages can't name MQTT)

## 2. Actor handlers

- [ ] 2.1 PollManager: track `_startedUtc` (constructor) and `_lastSuccessfulPollUtc` (on Changed/Unchanged outcomes). Handle `QueryPollHealth` returning `PollHealthResult`.
- [ ] 2.2 BudgetTracker: already handles `QueryBudgetStatus` — verify it returns everything the health check needs
- [ ] 2.3 MqttConnection: handle `QueryMqttConnectionStatus` returning `MqttConnectionStatusResult` from internal state

## 3. Rewrite health checks

- [ ] 3.1 PipelineHealthCheck: inject `IActorRef` for PollManager (via marker), Ask with 3s timeout, handle AskTimeoutException → Unhealthy. Keep Evaluate as static method taking PollHealthResult.
- [ ] 3.2 BudgetHealthCheck: inject BudgetTracker marker, Ask QueryBudgetStatus, handle timeout. Update Evaluate to take BudgetStatusResult directly.
- [ ] 3.3 MqttConnectionHealthCheck: inject MqttConnection marker, Ask QueryMqttConnectionStatus, handle timeout.

## 4. Delete state holders

- [ ] 4.1 Delete PollHealthState class and PollHealth record
- [ ] 4.2 Delete BudgetUsageState class and BudgetUsage record
- [ ] 4.3 Split MqttConnectionState: extract MqttSession (session counter for publisher), delete health-related fields
- [ ] 4.4 Remove state holder registrations from DI setup containers
- [ ] 4.5 Remove Volatile.Write calls from PollManager, BudgetTracker, MqttConnection

## 5. BudgetGaugeRegistration

- [ ] 5.1 Update BudgetGaugeRegistration to Ask BudgetTracker periodically instead of reading BudgetUsageState

## 6. Update tests

- [ ] 6.1 Update PipelineHealthCheck tests to use Ask pattern (TestProbe responds to QueryPollHealth)
- [ ] 6.2 Update BudgetHealthCheck tests
- [ ] 6.3 Update MqttConnectionHealthCheck tests
- [ ] 6.4 Update PollManager tests for new QueryPollHealth handler
- [ ] 6.5 Update MqttConnection tests for new QueryMqttConnectionStatus handler
- [ ] 6.6 Update BudgetGaugeRegistration tests

## 7. Validation

- [ ] 7.1 `dotnet build Njord.slnx` — clean
- [ ] 7.2 Run all affected test projects
- [ ] 7.3 `dotnet run --project Njord.Architecture.Tests/Njord.Architecture.Tests.csproj` — all pass
- [ ] 7.4 `dotnet format Njord.slnx --verify-no-changes` — clean
- [ ] 7.5 Update test counts in AGENTS.md if changed
