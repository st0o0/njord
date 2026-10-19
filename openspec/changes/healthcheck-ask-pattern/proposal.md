## Why

Health checks currently use Volatile.Read/Write state holders to bridge actor state to the health check thread. This adds 3 intermediate state holder classes, keeps stale "last-known-good" data when actors are dead, and scatters state management across actors and singletons. The simpler Ask pattern eliminates the bridge: health checks ask actors directly, and an unresponsive actor correctly reports as Unhealthy.

## What Changes

- PipelineHealthCheck: Ask PollManager with new QueryPollHealth message instead of reading PollHealthState
- BudgetHealthCheck: Ask BudgetTracker with existing QueryBudgetStatus instead of reading BudgetUsageState
- MqttConnectionHealthCheck: Ask MqttConnection with new QueryMqttConnectionStatus instead of reading MqttConnectionState
- Delete PollHealthState, BudgetUsageState classes and PollHealth, BudgetUsage records
- Split MqttConnectionState: Session counter moves to MqttSession (publisher only), health fields deleted
- BudgetGaugeRegistration: Ask BudgetTracker periodically instead of reading BudgetUsageState
- All 3 checks handle AskTimeoutException → Unhealthy
- Keep the static Evaluate pattern (testability)

## Non-goals

- Not changing HealthOptions (separate cleanup)
- Not changing JournalHealthCheck or DataDirectoryHealthCheck (they probe filesystems directly, no actors)
- Not changing the /healthz and /alive endpoint mapping

## Capabilities

### New Capabilities

- `healthcheck-ask`: Actor-queried health checks with timeout-based failure detection

### Modified Capabilities

_(none)_

## Impact

- Njord.Messages: 2 new message pairs (QueryPollHealth/PollHealthResult, QueryMqttConnectionStatus/MqttConnectionStatusResult)
- Njord.Pipeline: PollManager handles QueryPollHealth, PollHealthState deleted
- Njord.Budget: BudgetUsageState deleted, BudgetGaugeRegistration uses Ask
- Njord.Mqtt: MqttConnectionState split, MqttConnection handles QueryMqttConnectionStatus
- Tests: health check specs simplified (no state holder setup), new Ask-based patterns
