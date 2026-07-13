## Context

The pipeline currently drops `FetchOutcome.Failure` silently via `.Collect(Success)`
in `PipelineActor`. The scheduler never learns a fetch failed, so the next attempt
is the regular poll interval (default 60 min). Meanwhile, the egress actor carries
tombstone-queue infrastructure, `RefreshModel`/`RefreshLocation` handlers exist
without producers, and `ParameterDef.FriendlyName` is populated but never read.
HA Discovery is hardwired on with no way to disable it.

## Goals / Non-Goals

**Goals:**

- Route `FetchOutcome.Failure` back to the scheduler so transient errors trigger
  faster re-polls instead of waiting a full interval.
- Remove dead code that adds complexity without value (tombstone queue, refresh
  commands, unused fields).
- Make HA Discovery opt-out via `MqttOptions.DiscoveryEnabled`.

**Non-Goals:**

- Adding Polly or new retry abstractions — the scheduler's existing backoff is
  reused.
- Tombstone logic for future config changes (added when needed).
- Changing the fetch logic itself or the `OpenMeteoClient`.

## Decisions

### 1. Widen BroadcastHub to `FetchOutcome` (not just `Success`)

**Choice:** Change `BroadcastHub.Sink<FetchOutcome.Success>` →
`BroadcastHub.Sink<FetchOutcome>` in PipelineActor. Move the `.Collect(Success)`
filter downstream into each consumer that only wants successes.

**Why not stream-level retry (`.RetryWhen` / `RestartFlow`):** Akka.Streams has
no element-level retry operator. `RestartSource`/`RestartFlow` restart the entire
sub-graph, not individual elements. A task-level retry inside `SelectAsync` would
block a parallelism slot during the backoff delay, and the scheduler already has
per-model backoff logic — duplicating it in the stream would create competing
retry loops.

**Why not null-return instead of Failure type:** The structured `FetchFailureReason`
enum lets the scheduler make reason-specific decisions (rate-limited vs. transport
vs. permanent). Returning null loses that information.

### 2. Scheduler failure routing by reason

The scheduler's BroadcastHub consumer handles `FetchOutcome` directly:

- **Success** → existing `HashResult` path (compute hash, persist, schedule).
- **Failure(Transport)** → treat as a miss: increment `missCount`, schedule
  retry with existing exponential backoff (1m, 2m, 4m… capped 15m).
- **Failure(RateLimited)** → schedule retry at `max(5 min, normal backoff)`,
  log warning. This respects the API's rate signal without hammering.
- **Failure(ModelUnavailable | MalformedPayload)** → permanent for this cycle,
  no retry. Log warning, wait for next regular poll.

The `FetchOutcome.Failure` record carries `Location` and `Model` from the
`WeightedTarget` context. Since `SelectAsyncUnordered` processes targets and
returns `FetchOutcome`, the location/model context needs to travel with the
failure. **Decision:** add `Location` and `Model` fields to `FetchOutcome.Failure`
(the client already knows these from its parameters).

### 3. Feedback consumer stays Success-only

The hash-feedback consumer in PipelineActor only makes sense for successes
(you can't hash a failure). It filters with `.Collect(Success)` locally.

### 4. Tombstone queue removal

Remove entirely: `_tombstoneQueue`, `_ownConfigTopics` HashSet, `_deviceConfigFilter`,
the wildcard subscription on `<prefix>/device/+/config`, and the stale-config
matching in `OnInboundAsync`. The `OnInboundAsync` handler reduces to HA birth
detection only (and is skipped entirely when discovery is disabled).

### 5. Discovery toggle

`MqttOptions.DiscoveryEnabled` (default `true`). Guards in `MqttEgressActor`:

- `OnConnectedAsync`: skip HA status subscription + `PublishDiscovery()` when
  disabled.
- `OnInboundAsync`: skip entirely when disabled (only HA birth remains after
  tombstone removal).
- `_discoveryQueue` materialization: still materialized (simplifies graph setup),
  but never offered into.

No validation rule — `false` is always valid.

### 6. Dead code removal scope

| Item | Action |
|------|--------|
| `RefreshModel` / `RefreshLocation` | Remove from `PipelineCommand`, remove handlers from `SchedulerActor` (both `Command<>` registrations in constructor and `BecomeReady`), remove `OnRefreshModel` / `OnRefreshLocation` methods. |
| `ParameterDef.FriendlyName` | Remove from record, remove from all `BuildAll()` entries in `ParameterRegistry`. |
| `ParameterRegistry.GetByApiName(string, ParameterGranularity)` | Remove the two-parameter overload. |
| `ParameterRegistry.All` / `GetByGroup()` | Keep — used by tests, zero cost. |

## Risks / Trade-offs

- **[BroadcastHub type widening]** Every consumer now receives all outcomes
  including failures. Consumers that only want successes must filter explicitly.
  → Mitigated: only two consumers exist (egress, feedback), both already need
  filtering; the pattern is `.Collect()` which is already in use.

- **[Failure carries location/model]** Adding fields to `FetchOutcome.Failure`
  means the client produces slightly more data per failure.
  → Negligible: failures are rare, the fields are two small strings.

- **[RateLimited retry at 5 min]** If the API is truly rate-limiting, even 5 min
  may be too aggressive. → The 5 min minimum is conservative relative to the
  API's 600/min limit; a single retry after 5 min is one request.

- **[Discovery disabled but HA expected]** If someone sets `DiscoveryEnabled=false`
  and expects HA entities, they get state topics but no auto-discovered entities.
  → Default is `true`; the flag is intentional opt-out.
