## Context

`Njord.Core` holds 88 files spanning two distinct concerns: pure mathematical computation (consensus, alerts, trends, indices, history — 46 files in `Analysis/`) and infrastructure plumbing (options, validators, actor keys, metrics, stream supervision — 42 files). The Analysis types depend only on `Njord.Domain.Weather` records and 5 small Options POCOs. Extracting them creates a clean separation matching FunkArr's pattern where Core is thin plumbing.

## Goals / Non-Goals

**Goals:**
- Pure math library (`Njord.Compute`) with zero Akka/DI dependencies
- Pure xUnit test project (`Njord.Compute.Tests`) — fastest in the suite
- Core reduced to infrastructure plumbing
- Architecture tests enforce the new layer

**Non-Goals:**
- Changing computation logic or test assertions
- Moving non-Analysis types (BudgetCalculator, HorizonProjection, TopicSlug, StreamSupervision)
- Renaming namespaces beyond what's needed for options types

## Decisions

### D1: Options POCOs move with their consumers

**Decision**: Move `AlertOptions`, `HistoryOptions`, `IndexOptions`, `IndexPreferences`, `LocationIndexOverride` from `Njord.Core/Configuration/` to `Njord.Compute/Configuration/`, keeping namespace `Njord.Configuration`.

**Alternative A**: Leave options in Core, have Compute reference Core. Rejected — creates a circular concern (math library depending on plumbing).

**Alternative B**: Move options to Domain. Rejected — they're configuration with defaults and setters, not domain value objects.

**Why**: These POCOs define computation parameters (thresholds, retention days, preference weights). They belong with the computations they parameterize. Keeping `Njord.Configuration` namespace means zero consumer changes — Core and all feature libs already `using Njord.Configuration`.

### D2: Njord.Compute in /Foundation/ solution folder

**Decision**: Place `Njord.Compute` in the `/Foundation/` folder of `Njord.slnx`, alongside Domain, Persistence, Messages, Core.

**Why**: Compute is a foundation library — not a feature library, not the host. It sits between Domain and Core in the dependency chain.

### D3: Keep Njord.Analysis namespace

**Decision**: All Analysis types keep `namespace Njord.Analysis` — no rename to `Njord.Compute.Analysis`.

**Why**: Zero consumer changes. The namespace already reflects the domain concept, not the assembly name. Multiple assemblies sharing a namespace root is standard .NET practice.

### D4: Core.Tests Analysis/ directory moves entirely

**Decision**: Move all 14 test files from `Njord.Core.Tests/Analysis/` to `Njord.Compute.Tests/Analysis/`. Update namespaces to `Njord.Compute.Tests.Analysis`.

**Why**: Test project matches production project 1:1. The Analysis specs have no Akka TestKit dependency — they're pure function tests with `[Fact]` and `[Theory]`, no `ConfigureAkka` overrides.

### D5: Architecture test updates

**Decision**:
- `NjordArchitecture.BaseLibraryNames`: add `"Njord.Compute"`
- `NjordArchitecture.CoreAndBelow` (used in `LayerReferenceSpec`): add `"Njord.Compute"`
- New test: `Compute_references_only_Domain()`
- Modified test: `Core_references_only_Domain_Messages_and_Persistence()` becomes `Core_references_only_Domain_Messages_Persistence_and_Compute()`

### D6: Njord.Compute.Tests project setup

**Decision**: Minimal csproj — `Exe` output, `IsTestProject`, references to `Njord.Compute` and `xunit.v3.mtp-v2` only. No `Akka.Hosting.TestKit`, no `Njord.Tests.Shared` (Analysis specs don't use shared fakes).

**Verification needed during implementation**: Confirm none of the 14 Analysis test files reference `FakeOpenMeteoClient`, `TestPersistenceConfig`, `TestTimeouts`, or other shared helpers. If any do, add the `Njord.Tests.Shared` reference.

## Risks / Trade-offs

- **[Namespace `Njord.Configuration` spans two assemblies]** → Acceptable. `AlertOptions` in Compute and `AlertOptionsValidator` in Core both use `Njord.Configuration`. Consumers already `using Njord.Configuration` — no changes needed. Type resolution is unambiguous.
- **[14 test files to move]** → Mechanical. Namespace update from `Njord.Core.Tests.Analysis` to `Njord.Compute.Tests.Analysis` is the only change per file.
- **[Transitive dependency growth]** → Feature libs gain Compute transitively through Core. This is desirable — they already use Analysis types. No new explicit references needed.

## Open Questions

None — the extraction is mechanical with well-understood boundaries.
