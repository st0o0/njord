## Context

Currently 4 feature libraries have their tests in the host test project
`Njord.Tests`. This mirrors the project's history — before the recent library
extraction (`refactor: extract Mqtt and Enrichment libraries`), these tests were
host-resident by necessity. The extraction refactored production code into
separate projects but left the tests behind.

## Goals / Non-Goals

**Goals:**
- 1:1 test project mapping for all feature libraries
- Verify snapshots move with their specs
- `Njord.Tests` retains only host-specific tests
- All tests pass identically after extraction

**Non-Goals:**
- Changing test logic or structure
- Splitting the Persistence golden-master test — it depends on Enrichment types
  and is fine in Njord.Tests (or could move with Enrichment.Tests)

## Decisions

### New csproj files copy the pattern from existing test projects

Each new test project copies its csproj from an existing one (e.g.,
`Njord.Egress.Tests.csproj`), adjusting only the project reference to point at
the correct production library. All test projects share `Directory.Build.props`
for TFM, packages, and test infrastructure.

**Alternative considered:** A shared test project template. Rejected — the
projects already share everything through `Directory.Build.props` and
`Directory.Packages.props`; a template would add indirection without benefit.

### Namespace follows project name

Moved files update their namespace from `Njord.Tests.Mqtt` → `Njord.Mqtt.Tests`
(etc.) to match the project name, consistent with other test projects.

### Verify snapshots are co-located

Verify `.verified.txt` files move alongside their spec files. Verify resolves
snapshot paths relative to the source file, so no path configuration is needed.

### Persistence golden-master stays in Njord.Tests

`ForecastHistoryDtoSerializationSpec` tests persistence DTOs used by the
Enrichment feature. It references both Njord.Persistence and Njord.Enrichment.
Keeping it in `Njord.Tests` avoids adding a cross-domain test reference.

### PollPipelineSpec stays in Njord.Tests

`PollPipelineSpec` is a host-level integration test that exercises the full
pipeline including actors from multiple feature libraries. It belongs in the
host test project.

## Risks / Trade-offs

- [Low] Architecture tests use `Njord.*.Tests` wildcard — new projects are
  automatically included → Verified by `DisabledTestArchitectureSpec` picking
  up new assemblies.
- [Low] ModuleInitializer.cs and EnrichmentActorCollection.cs may need
  duplication or splitting → Check if they are shared or scoped to one area.
