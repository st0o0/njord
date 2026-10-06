## Why

`Njord.Core` is 88 files mixing two concerns: pure mathematical computation (`Analysis/` — 46 files, ~174 tests) and infrastructure plumbing (`Configuration`, `Actors`, `Diagnostics` — 42 files). FunkArr.Core is thin plumbing, with domain computation in dedicated projects. Extracting the Analysis layer into `Njord.Compute` creates a pure math library with zero Akka/DI dependencies, whose tests are the fastest in the suite. Core becomes what FunkArr.Core is: options validation, actor keys, metrics.

## What Changes

- **New project `Njord.Compute`** — references `Njord.Domain` only; contains all 46 Analysis files plus 5 compute-parameter Options POCOs (`AlertOptions`, `HistoryOptions`, `IndexOptions`, `IndexPreferences`, `LocationIndexOverride`) moved from `Njord.Core/Configuration/`. Namespace `Njord.Analysis` stays unchanged.
- **New test project `Njord.Compute.Tests`** — 14 test files (~174 pure mathematical tests) moved from `Njord.Core.Tests/Analysis/`. No Akka, no TestKit, no DI — plain xUnit only.
- **Modified `Njord.Core`** — loses `Analysis/` (46 files) and 5 Options POCOs; gains `ProjectReference` to `Njord.Compute`. Validators (`AlertOptionsValidator`, `HistoryOptionsValidator`, `IndexOptionsValidator`) stay in Core, reference Compute for the options types. Shrinks from ~88 to ~42 files.
- **Modified `Njord.Core.Tests`** — loses 14 Analysis test files (~174 tests), retains ~167 tests for configuration, budget, diagnostics, stream supervision.
- **Modified architecture tests** — `Njord.Compute` added to `BaseLibraryNames` and `CoreAndBelow`; `LayerReferenceSpec` gains a test verifying Compute references only Domain; Core's allowed references expanded to include Compute.

## Non-goals

- Changing any computation logic or test assertions
- Moving non-Analysis types out of Core
- Changing the public API surface of any type
- No API-budget impact — no polling changes

## Capabilities

### New Capabilities
- `compute-library`: Extraction of pure computation into an independent library with its own test project

### Modified Capabilities
- `architecture-zone-enforcement`: Layer reference rules updated for the new Compute foundation library
- `test-project-structure`: Solution project list and test organization updated

## Impact

- **New projects**: `src/Njord.Compute/`, `src/Njord.Compute.Tests/`
- **Modified projects**: `Njord.Core` (loses 51 files), `Njord.Core.Tests` (loses 14 files)
- **Modified**: `Njord.slnx` (2 new projects in `/Foundation/` and `/Tests/`)
- **Modified**: `Njord.Architecture.Tests` (layer rules, base library list)
- **Modified**: `AGENTS.md` (solution structure, test counts)
- Feature libs referencing Core gain transitive access to Compute — no csproj changes needed
