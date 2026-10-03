## Why

Fifteen `PipeTo` calls across eight production actors omit the `failure:` mapper.
When `GetActorAsync` or `Task.WhenAll` fails, Akka wraps the exception in
`Status.Failure` and sends it to the actor's mailbox. Every affected actor has a
`ReceiveAny(_ => Stash.Stash())` catch-all that silently stashes the failure —
it is never handled or logged. This violates the project's "no
`Status.Failure`/`Status.Success`" convention and masks dependency-resolution
failures at runtime.

## What Changes

- Add `failure:` mapper to every `PipeTo` call that currently omits it, using a
  private `XxxResolveFailed(Exception Cause)` record per resolve path.
- Handle the failure record in `ConfigureWaitingForRefs()` (for
  `StreamConsumerActor` subclasses) or the corresponding init behavior (for
  `PipelineActor` and `SchedulerActor`) by logging the error and calling
  `ScheduleRetryResolve()` (or the equivalent backoff retry already present).

No new public messages, no API changes, no behavioral change beyond proper
error logging and retry on dependency-resolution failure.

## Capabilities

### New Capabilities

- `actor-resolve-failure-handling`: Explicit failure handling on all actor
  dependency-resolution `PipeTo` calls — project-owned failure records,
  logging, and retry on `GetActorAsync` failures.

### Modified Capabilities

(none)

## Non-goals

- Changing the `StreamConsumerActor` base class itself (the `ReceiveAny` stash
  catch-all is correct; the fix is in the subclasses).
- Adding failure handlers to `PipeTo` calls that already have them (StreamRef
  and OfferAsync calls in `PipelineActor`, `SchedulerActor` are correct).
- Restructuring the dependency-resolution pattern (it works; only the error path
  is incomplete).

## Impact

- **Files changed:** 8 actor files across 5 feature libraries (Grpc, Egress,
  Enrichment, Mqtt, Pipeline).
- **Test files changed:** Corresponding spec files to verify failure handling.
- **API budget:** Zero — no polling or HTTP changes.
- **Risk:** Low — adds error-path handling; happy path unchanged.
