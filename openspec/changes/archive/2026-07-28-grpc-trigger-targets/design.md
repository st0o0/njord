## Context

`ConfigService` already has `TriggerPoll(location, model)` for firing immediate polls and
`GetStatus` which returns poll state per model (among other server metadata). Discovering
triggereable targets currently requires either the two-call `GetLocations` → `GetModels`
flow (which lacks poll state) or `GetStatus` (which bundles unrelated server info).

The `SchedulerActor` already responds to `GetPollStates` with a `PollStatesSnapshot`
containing all location/model pairs and their poll state — this is the same data source
used by `GetStatus`.

## Goals / Non-Goals

**Goals:**

- Single unary RPC returning all configured location/model pairs with poll state.
- Use `google.protobuf.Timestamp` for temporal fields in the new message.
- Reuse existing `SchedulerActor`/`GetPollStates` path — no new actor messages.

**Non-Goals:**

- Migrating existing `ModelStatus` to use `Timestamp` (separate change).
- Including model metadata (display name, provider, coverage) — stays in `ForecastService.GetModels`.
- Streaming variant.

## Decisions

### Use `google.protobuf.Timestamp` for temporal fields

The new `TriggerTarget` message uses `Timestamp` for `next_poll` and `last_change`
instead of `int64` unix seconds. This gives native `DateTimeOffset` conversion in C#
via `Timestamp.FromDateTimeOffset()` / `.ToDateTimeOffset()` and avoids ambiguity
about second vs. millisecond precision.

Alternative: raw `int64` for consistency with `ModelStatus`. Rejected because new
messages should use well-known types; existing messages can migrate separately.

### Place RPC in ConfigService

`GetTriggerTargets` is an operations concern (what can I trigger?) not a forecast read.
It lives next to `TriggerPoll` in `ConfigService`.

Alternative: new dedicated service. Rejected — adds proto/service overhead for one RPC.

### Ask SchedulerActor directly (same as GetStatus)

The handler sends `GetPollStates` to SchedulerActor and maps the `PollStatesSnapshot`
entries to `TriggerTarget` messages. Same pattern as `GetStatus`, same timeout handling.

## Risks / Trade-offs

- [Stash during startup] SchedulerActor stashes `GetPollStates` until steady state.
  `GetTriggerTargets` inherits this — returns empty/times out during warmup.
  → Acceptable: same behavior as `GetStatus` and `TriggerPoll`.
- [Proto size] Adding `Timestamp` import increases generated code slightly.
  → Negligible: `google/protobuf/timestamp.proto` is a standard well-known type.
