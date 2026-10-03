## Context

All `StreamConsumerActor` subclasses resolve dependencies via
`GetActorAsync<IXxxActor>().PipeTo(Self, success: r => new XxxResolved(r))`.
The `success:` mapper converts the resolved `IActorRef` into a private record;
the `failure:` mapper is missing on 15 calls. When `GetActorAsync` fails, Akka
wraps the exception in `Status.Failure`, which hits the `ReceiveAny` stash
catch-all and is silently swallowed — no log, no retry.

`PipelineActor` and `SchedulerActor` are not `StreamConsumerActor` subclasses
but follow the same pattern with their own init behaviors.

## Goals / Non-Goals

**Goals:**
- Every `PipeTo` on a resolve task provides a `failure:` mapper
- Failures are logged at Warning and retried via existing backoff
- Zero behavioral change on the happy path

**Non-Goals:**
- Changing the `StreamConsumerActor` base class
- Adding failure handlers to `PipeTo` calls that already have them
- Introducing a generic resolve-failure mechanism in the base class

## Decisions

### One private failure record per resolve path

Each `GetActorAsync` call gets its own `private sealed record XxxResolveFailed(Exception Cause)`.
This matches the existing pattern where each resolve path already has its own
success record (e.g., `EgressResolved`, `PipelineResolved`).

**Alternative considered:** A single shared `ResolveFailed` record in the base
class. Rejected because it would require discriminating which dependency failed,
and the base class pattern is intentionally minimal.

### Reuse existing retry infrastructure

`StreamConsumerActor` subclasses already have `ScheduleRetryResolve()` with
exponential backoff. `SchedulerActor` already has `RetryPipelineResolve` with
backoff. The failure handlers call into these existing mechanisms.

**Alternative considered:** Letting the `BackoffSupervisor` handle it via actor
restart. Rejected because resolve failures are transient (registry not ready yet)
and the actor already has fine-grained retry — a full restart would lose
accumulated state (materialized streams, tracked dependencies).

### Handler placement in ConfigureWaitingForRefs

For `StreamConsumerActor` subclasses, the failure handler is registered in
`ConfigureWaitingForRefs()` alongside the existing success handler. This keeps
success and failure handling co-located.

## Risks / Trade-offs

- [Low] Adding 15 records and 15 handlers is mechanical but increases line count
  → The records are one-liners; the handlers are two lines (log + retry).
  Net effect is about 3-5 lines per actor.
- [Low] If `GetActorAsync` fails repeatedly, backoff caps at 30s per
  `RetryBackoff` → This is the existing behavior for other transient failures;
  no change needed.

## Open Questions

(none)
