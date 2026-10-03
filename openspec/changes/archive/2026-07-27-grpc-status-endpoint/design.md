## Context

`GetStatus` in `ConfigGrpcService` already returns version, uptime, and budget
aggregates. The proto already defines `ModelStatus` and `repeated ModelStatus
models = 4` on `ServerStatus` — but it's never populated. There's a TODO on line
91 of `ConfigGrpcService.cs` acknowledging the gap.

The SchedulerActor holds a `Dictionary<string, ModelPollState>` keyed by
`"location|model"` with rich per-model data (phase, next poll, miss count,
learned cycle). This data is locked inside the actor. The existing
`TriggerImmediatePoll` Ask pattern proves the approach works.

`EnrichmentOptions` is available via DI as `IOptionsMonitor<NjordOptions>`,
already injected into ConfigGrpcService.

## Goals / Non-Goals

**Goals:**

- Populate the existing `ModelStatus` in `ServerStatus` with live poll state data
- Add `active_enrichments` string list to `ServerStatus`
- Establish a query protocol to read SchedulerActor state without side effects

**Non-Goals:**

- MQTT connection state (internal egress detail)
- Rate-limiter token balance (implementation detail of the stream)
- Streaming status updates
- Changing the existing `BudgetStatus` shape

## Decisions

### 1. Replace `ModelStatus` proto rather than add a new message

The existing `ModelStatus` (location, model, last_fetch_timestamp,
consecutive_failures, state as string) was never populated. Rather than adding a
second message, rework `ModelStatus` in place to match the actual `ModelPollState`
fields. Since no client uses this yet, there's no backwards-compatibility concern.

New shape:
```
message ModelStatus {
  string location = 1;
  string model = 2;
  string phase = 3;               // "discovery" or "steady"
  int64 next_poll_utc = 4;        // unix seconds
  optional int64 last_change_utc = 5;
  int32 miss_count = 6;
  optional int64 cycle_seconds = 7;
}
```

**Why not keep the old fields?** `last_fetch_timestamp` conflated "last poll" with
"last data change" — `last_change_utc` is more precise. The string `state` was
never defined; `phase` (Discovery/Steady) is the actual domain concept. The
`consecutive_failures` maps to `miss_count`.

### 2. Ask protocol (not shared-state singleton)

Add `GetPollStates` query message → SchedulerActor responds with
`PollStatesSnapshot`. This mirrors the existing `TriggerImmediatePoll` pattern.

**Alternative considered:** A `PollStatusSnapshot` singleton that the actor
publishes to on every state change. Rejected because:
- Adds eventual consistency without benefit (status is polled rarely)
- Requires the actor to publish on every hash result / miss / failure
- `TriggerImmediatePoll` already proves Ask works for this actor

The 5s Ask timeout is acceptable — `GetStatus` is a UI-refresh call, not a
hot-path operation.

### 3. `GetStatus` becomes async

Currently `GetStatus` returns `Task.FromResult(...)`. With the Ask, it becomes
`async Task<ServerStatus>`. If the SchedulerActor is unreachable (timeout), the
response still returns version/uptime/budget with an empty model list — graceful
degradation.

### 4. Enrichment features as simple string list

Add `repeated string active_enrichments = 5` to `ServerStatus`. Populated by
checking which of the 7 enrichment feature flags are enabled in
`EnrichmentOptions`. Values: `"consensus"`, `"alerts"`, `"derived"`, `"trends"`,
`"indices"`, `"energy"`, `"history"`.

**Why not a dedicated message?** The full config is already available via
`GetConfig` with `DetailedEnrichmentConfig`. This is just a convenience field for
quick "what's on" checks.

### 5. Handler placement in SchedulerActor

`GetPollStates` is handled in the `Ready` behavior only. In `WaitingForRefs`,
`Connecting`, and `WaitingForConnection`, the query is stashed alongside other
commands — when the actor reaches Ready, the stash is unstashed and the query
gets answered. This avoids returning partial state during startup.

## Risks / Trade-offs

**[Ask timeout during actor restart]** → The SchedulerActor may be recovering
from persistence on startup. The gRPC service catches `AskTimeoutException` and
returns status with empty model list. The HA plugin should handle an empty list
gracefully.

**[Proto field renumbering]** → Reworking `ModelStatus` changes field semantics
on existing field numbers (3, 4, 5). Since no client ever received data on these
fields, this is safe. If a future client used an old proto definition, they'd see
wrong types — but the message was never populated, so no old client could exist.

## Open Questions

None — all decisions made during explore.
