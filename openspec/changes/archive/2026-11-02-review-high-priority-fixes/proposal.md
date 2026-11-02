## Why

A full project review identified two high-priority gaps: the GrpcConsumer silently stops receiving pipeline events after a stream completion (no reconnect), and two default-on features (ForecastModelComparison, EnsembleModelComparison) have zero test coverage. Both reduce confidence in production resilience and correctness.

## What Changes

- GrpcConsumer reconnects to the pipeline on `StreamCompleted` instead of logging a warning and going deaf
- GrpcConsumer PreStart switches from `GetActorAsync` to synchronous `GetActor` for consistency with the rest of the codebase
- Add unit tests for `ModelComparisonHelper.CompareDays` (the shared logic)
- Add rule-level tests for `ForecastModelComparison` and `EnsembleModelComparison` (projection + evaluation)

## Capabilities

### New Capabilities

- `grpc-consumer-reconnect`: GrpcConsumer automatically re-attaches to the pipeline after stream completion
- `model-comparison-tests`: Test coverage for ForecastModelComparison, EnsembleModelComparison, and their shared helper

### Modified Capabilities

(none)

## Non-goals

- Changing the GrpcConsumer's BroadcastHub or client-facing stream behavior
- Adding reconnect logic for other consumers (Gateway already handles this differently)
- Testing GranularAqi or RagweedPollen (lower priority, separate change)
- Changing ModelComparisonHelper logic itself

## Impact

- `src/Njord.Grpc/GrpcConsumer.cs`: reconnect handler, PreStart simplification
- `src/Njord.Grpc.Tests/GrpcConsumerTests.cs`: new reconnect test
- `src/Njord.Features.Tests/`: new test files for ModelComparisonHelper, ForecastModelComparison, EnsembleModelComparison
