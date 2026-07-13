## Why

The egress and pipeline subsystem carries dead code from pre-release iteration:
a tombstone queue that cleans up retained MQTT configs that have never been
published, `RefreshModel`/`RefreshLocation` commands with no producer, and
unused record fields. Meanwhile, `FetchOutcome.Failure` is structured data that
gets silently dropped — a transient HTTP error costs a full poll interval
(default 60 min) of missed data because the scheduler never learns the fetch
failed. Finally, HA Discovery is hardwired on, but some deployments may only
want the raw MQTT state topics without Home Assistant integration.

## What Changes

- **Remove dead code:** tombstone queue infrastructure in `MqttEgressActor`
  (`_tombstoneQueue`, `_ownConfigTopics`, `_deviceConfigFilter`, config-topic
  subscription, stale-config detection in `OnInboundAsync`); `RefreshModel` /
  `RefreshLocation` commands and handlers in `SchedulerActor` / `PipelineCommand`;
  `ParameterDef.FriendlyName` (never read); `ParameterRegistry.GetByApiName`
  two-parameter overload (never called).
- **Make HA Discovery optional:** add `MqttOptions.DiscoveryEnabled`
  (default `true`). When `false`, no discovery configs are published, no HA
  status subscription, no birth re-publish — availability and state topics
  still work.
- **Route failures to the scheduler:** widen `BroadcastHub` from
  `FetchOutcome.Success` to `FetchOutcome`, let the scheduler consume failures
  and re-schedule based on reason (`Transport` → backoff retry,
  `RateLimited` → longer hold-off, `ModelUnavailable`/`MalformedPayload` →
  skip until next regular poll).

## Non-goals

- Adding new retry primitives or Polly-style resilience — the scheduler's
  existing backoff mechanism is sufficient once it receives failure signals.
- Tombstone logic for future config changes — will be added if/when a
  breaking config change ships to a live deployment.
- Consensus device or cross-model aggregation.

## Capabilities

### New Capabilities

- `failure-routing`: Routing `FetchOutcome.Failure` from the pipeline back to
  the scheduler for reason-based retry/skip decisions.
- `discovery-toggle`: Making HA MQTT Discovery an opt-in/opt-out configuration
  flag.

### Modified Capabilities

- `mqtt-egress`: Tombstone queue removal simplifies the egress graph and
  `OnInboundAsync` handler.
- `poll-scheduler`: Scheduler gains a failure-consumption path alongside the
  existing `HashResult` path; `RefreshModel`/`RefreshLocation` handlers removed.
- `pipeline-actor`: `BroadcastHub` type widens from `FetchOutcome.Success` to
  `FetchOutcome`; `.Collect()` moves downstream to consumers.
- `pipeline-commands`: `RefreshModel` and `RefreshLocation` command records
  removed.
- `parameter-registry`: `FriendlyName` removed from `ParameterDef`;
  two-parameter `GetByApiName` overload removed.

## Impact

- **Production code:** `MqttEgressActor`, `PipelineActor`, `SchedulerActor`,
  `PipelineCommand`, `MqttOptions`, `ParameterDef`, `ParameterRegistry`.
- **Tests:** `SchedulerActorSpec` (remove refresh tests, add failure-routing
  tests), `ParameterRegistrySpec` / `ParameterOptionsValidationSpec` (adapt to
  removed fields), `PollPipelineSpec` (adapt to `BroadcastHub<FetchOutcome>`),
  new tests for discovery toggle.
- **Configuration:** new env var `Njord__Mqtt__DiscoveryEnabled` (default
  `true` — no change for existing setups).
- **API budget:** no change — failure retry reuses the existing throttle in
  the pipeline graph; no additional API calls beyond what the scheduler would
  have made on the next regular tick.
