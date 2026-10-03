## Why

The codebase has grown organically with multiple types per file and static `Compute` factory methods on data records. This violates .NET conventions (one type per file, file named after type) and makes the domain harder to navigate. The `Record.Compute()` pattern mixes data and computation, preventing dependency injection and making the enrichment pipeline harder to test in isolation.

## What Changes

- **File-per-type**: Split 10 multi-type files so each `.cs` file contains exactly one public/internal type. Fix 2 filename/type mismatches. Actor message records stay with their actor (Akka convention). ~20 new files, ~10 files reduced to single type.
- **Compute → Computer services**: Extract static `Compute` methods from 5 data records (`IndexResult`, `ConsensusSnapshot`, `TrendResult`, `DerivedResult`, `HistoryResult`) into dedicated Computer service classes. Register as singletons in DI. Enrichment features inject the computer instead of calling static methods.
- Pure math/scoring static classes (`IndexScorer`, `ConsensusComputer`, `DerivedComputer`, `TrendAnalyzer`, `HistoryAnalyzer`, `PreferenceResolver`, `AlertEvaluator`) remain static — they have no dependencies and no state.

## Non-goals

- No behavioral changes — all computation logic stays identical.
- No new tests — existing tests cover all logic; only call sites change.
- No API-budget impact — pure refactoring.
- No changes to pure static math/scoring classes.

## Capabilities

### New Capabilities

_(none — structural refactoring only)_

### Modified Capabilities

_(none — no requirement-level changes, only code organization)_

## Impact

- **Domain/Analysis**: 10 files split, 5 new Computer classes, 5 records lose their `Compute` methods.
- **Enrichment/Features**: All 5 enrichment features updated to inject and call Computer services instead of static `Record.Compute()`.
- **DI registration**: 5 new singleton registrations in service setup.
- **Tests**: Call site updates where tests invoke `Compute` directly.
