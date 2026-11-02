## Context

A project review identified two high-priority gaps. The GrpcConsumer is the sole consumer of the pipeline's BroadcastHub output -- when its SourceRef stream ends (pipeline actor restart via BackoffSupervisor), it logs a warning and stops receiving events permanently until its own actor restarts. Separately, ForecastModelComparison and EnsembleModelComparison are default-on features whose shared logic (`ModelComparisonHelper`) has no tests.

## Goals / Non-Goals

**Goals:**
- GrpcConsumer self-heals after pipeline stream loss without requiring its own restart
- Align GrpcConsumer's pipeline resolution with the synchronous pattern used everywhere else
- Cover the model comparison logic and both rules with tests

**Non-Goals:**
- Changing the BroadcastHub/client-facing stream architecture
- Adding reconnect logic for Gateway or PollWorker (they use SinkRefs, different pattern)
- Changing ModelComparisonHelper logic itself
- Testing GranularAqi/RagweedPollen (separate change)

## Decisions

### 1. Synchronous GetActor instead of GetActorAsync in PreStart

All other actors (PollManager, Gateway) use `Context.GetActor<T>()` which returns immediately. The BackoffSupervisor wrapper is registered before the child actors start, so the reference is always available. The async pattern in GrpcConsumer was unnecessary and introduced two extra message types (`PipelineResolved`, `PipelineResolveFailed`) that can be eliminated.

**Alternative considered:** Keep async with retry. Rejected because the synchronous approach is simpler, consistent, and the BackoffSupervisor reference is stable by design.

### 2. StreamCompleted triggers re-resolution + re-request

On `StreamCompleted`, call `Context.GetActor<IForecastPipeline>()` and send `RequestPipelineOutput(0)`. The existing `OnSourceRef` handler materializes the new stream. No delay needed -- the BackoffSupervisor reference is stable even during child restart.

If `GetActor` throws (actor not registered -- should not happen in practice), the exception reaches the GrpcConsumer's own supervisor, which restarts it via BackoffOnFailure. This is the correct fallback: a full PreStart re-initializes everything.

### 3. ModelComparisonHelper tests as the primary coverage target

Both rules delegate to `ModelComparisonHelper.CompareDays()`. The rules themselves are 4 lines each (project a dictionary, call helper, wrap result). Testing the helper thoroughly covers the logic; rule tests focus on projection (null when < 2 models) and the wiring.

### 4. Test file organization

- `Njord.Features.Tests/Forecasts/Shared/ModelComparisonHelperTests.cs` -- helper logic
- `Njord.Features.Tests/Forecasts/ForecastModelComparison/ForecastModelComparisonTests.cs` -- rule projection + evaluation
- `Njord.Features.Tests/Forecasts/EnsembleModelComparison/EnsembleModelComparisonTests.cs` -- rule projection + evaluation

Mirrors the production 1:1 layout convention.

## Risks / Trade-offs

- [Risk: GetActor throws during startup race] The BackoffSupervisor is registered before children start, and the GrpcConsumer starts after the pipeline. If this invariant breaks in a future refactor, GetActor would throw, the consumer restarts via its own supervisor, and retries. Self-healing, no data loss.
- [Risk: Rapid stream completion loops] If the pipeline keeps dying immediately, the consumer would loop: StreamCompleted -> reconnect -> StreamCompleted. Mitigated by the pipeline's own BackoffSupervisor which adds increasing delay between restarts, so the consumer's reconnect frequency is bounded by the pipeline's restart rate.
