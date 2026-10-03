## Why

Two methods in `Domain/Analysis` were superseded by newer implementations during the consensus-based enrichment rework but never cleaned up. They remain public, have dedicated tests, and create a false impression of active code paths. Removing them reduces maintenance surface and prevents confusion.

## What Changes

- Remove `IndexScorer.FrostProtection()` — worked from raw `ForecastSeries`, replaced by `IndexComputer.ComputeFrostFromConsensus()` which uses consensus data. No production callers.
- Remove `IndexComputer.BuildEnvelope(List<int>)` — computed envelopes from a list of per-model scores, replaced by `BuildEnvelopeFromBounds(int, int, double)` which uses CI bounds. No production callers.
- Remove corresponding test methods in `IndexScorerSpec` and `IndexResultSpec`.

## Non-goals

- No changes to the active frost or envelope logic.
- No consolidation of `AlertEvaluator.EvaluateFrost()` and `IndexComputer.ComputeFrostFromConsensus()` — they serve different purposes (alert severity vs index info).
- No API budget impact — this is a pure code removal, no polling changes.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

(none)

## Impact

- `src/Njord/Domain/Analysis/IndexScorer.cs` — method removal
- `src/Njord/Domain/Analysis/IndexComputer.cs` — method removal
- `src/Njord.Tests/Domain/Analysis/IndexScorerSpec.cs` — test removal
- `src/Njord.Tests/Domain/Analysis/IndexResultSpec.cs` — test removal (if `BuildEnvelope` tests exist there)
