## Why

The solution now has 10 production projects but still one test project, `src/Njord.Tests` (115 files, 825 tests, 14 folders mirroring all libraries). A change to `Njord.Domain` rebuilds and reruns every actor test, a test can reference any library regardless of where it belongs, and the architecture specs share an assembly with the tests they police. FunkArr already runs one test project per domain library plus an `Architecture.Tests` project; Njord should follow the same pattern now that the libraries exist.

## What Changes

- Split `src/Njord.Tests` into per-library xUnit v3 test projects, moving files with `git mv` (content unchanged except `namespace`/`using` lines forced by the move):
  - `src/Njord.Domain.Tests` (27 files)
  - `src/Njord.Persistence.Tests` (4 files + 5 Verify snapshots)
  - `src/Njord.Core.Tests` (17 files: Configuration, Diagnostics, `StreamConsumerActor`)
  - `src/Njord.Egress.Tests` (4 files)
  - `src/Njord.Grpc.Tests` (12 files)
  - `src/Njord.Pipeline.Tests` (18 files + 1 Verify snapshot)
  - `src/Njord.Architecture.Tests` (4 files)
- `src/Njord.Tests` stays as the host/leftover project (Mqtt, Enrichment, Health, host Configuration, the single-file Ingest and Sensors areas, `PollPipelineSpec`) until `extract-enrichment-mqtt-projects` lands.
- `src/Njord.Tests.Shared` stays one project and keeps referencing only `Njord.Core`; `FailingRefProvider` moves into it (without its host-only MQTT arms).
- One `xunit.runner.json` shared by all test projects, per-project `InternalsVisibleTo` entries on the production projects, `Njord.slnx` Tests folder updated.
- Docs: `AGENTS.md` (Build & test, Solution structure, architecture-test path), `README.md` test command, `.claude/skills/njord-*/SKILL.md` cited paths.

No production code, behavior, test logic or CI file changes. Total test count stays at the baseline of 825.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

None. This is a pure refactor of test project layout; no spec-level behavior changes (`skip_specs: true`).

## Impact

- Code: `src/Njord.Tests/**` (moved), 7 new test csproj files, `src/Njord.Tests.Shared/`, `src/Njord.slnx`, `InternalsVisibleTo` items in the production csproj files, `src/xunit.runner.json`.
- Docs: `AGENTS.md`, `README.md`, `.claude/skills/njord-actor-spec`, `njord-enrichment-feature`, `njord-persistent-actor`.
- CI: the shared workflow (`st0o0/github-workflows`) runs `dotnet test --solution` and picks up every test project from `Njord.slnx` automatically. No workflow file is touched; the user must confirm this behavior, including coverage merging and load, before the change is applied (see design, Decision 9).
- Interaction with `extract-enrichment-mqtt-projects` (blocked, not started): its tasks cite `src/Njord.Tests/{Architecture,Mqtt,Enrichment,Configuration}`; after this change `Architecture` and the Core configuration specs have moved. Land this change first and update those paths (task 10.4), or the other way round with the same one-line path edit.
- API budget: 0 requests (no polling change).

## Non-goals

- No behavior change, no production-code change beyond `InternalsVisibleTo` items.
- No CI or workflow change (discussed with the user first).
- No test rewrites: assertions, names, fixtures and timeouts stay as they are; fixing the known `!.` deviations or load-sensitive tests is out of scope.
- No split of the host-resident Mqtt/Enrichment tests (that belongs to `extract-enrichment-mqtt-projects`).
- No new production `AssemblyMarker` types, no new packages beyond moving existing `PackageReference` items.
