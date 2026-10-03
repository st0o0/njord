## Context

During the move from per-model `ForecastSeries` processing to consensus-based enrichment, two methods were superseded but not removed:

- `IndexScorer.FrostProtection()` iterates raw `ForecastSeries` to find first frost hour. The replacement `IndexComputer.ComputeFrostFromConsensus()` does the same from consensus data and is the only production path.
- `IndexComputer.BuildEnvelope(List<int>)` built score envelopes from per-model score lists. The replacement `BuildEnvelopeFromBounds(int, int, double)` computes envelopes from pessimistic/optimistic CI bounds directly.

Both dead methods are `public`/`internal static`, have test coverage, but zero production callers.

## Goals / Non-Goals

**Goals:**
- Remove dead methods and their tests
- Verify no production callers exist before removal

**Non-Goals:**
- Consolidating `AlertEvaluator.EvaluateFrost()` with `IndexComputer.ComputeFrostFromConsensus()`
- Changing any active logic or behavior

## Decisions

**Straight deletion over deprecation.** This is unreleased internal code with no external consumers. `[Obsolete]` attributes would be ceremony for dead code that nobody calls.

## Risks / Trade-offs

- [Risk: missed caller] → Mitigated by grep verification during explore phase; build will catch any missed reference at compile time.
- [Risk: test coverage gap] → The removed tests cover dead code paths. Active paths (`ComputeFrostFromConsensus`, `BuildEnvelopeFromBounds`) already have their own tests.
