## Why

The archived change `split-test-projects` replaced the single `Njord.Tests` project with per-library test projects, but the main specs `test-project-structure` and `architecture-zone-enforcement` still describe the old layout (one `Njord.Tests` project, one command to run everything). The specs now contradict the code.

## What Changes

- `test-project-structure`: the requirement "Unit and actor tests in Njord.Tests" becomes "Unit and actor tests in per-library test projects" (renamed and restated). Tests live in `Njord.<Name>.Tests` projects (Domain, Persistence, Core, Egress, Grpc, Pipeline, Architecture) plus the host-resident `Njord.Tests`; each is run with its own `dotnet run --project`.
- `test-project-structure`: "All test projects in the solution" lists every test project instead of only `Njord.Tests` and `Njord.Tests.Shared`.
- `architecture-zone-enforcement`: the test-class convention, the "covers all assemblies" rule and the "No disabled tests" rule are restated for `Njord.Architecture.Tests` covering every `Njord.*Tests` assembly (and `Njord.Tests.Shared` for disabled tests).
- Docs (`AGENTS.md`, `CLAUDE.md`, `.claude/skills/njord-*`) were checked for stale `Njord.Tests` claims; they already match the layout, so no edit is needed there unless the check finds drift.
- No code under `src/` changes.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `test-project-structure`: test projects are per library; one requirement renamed, one restated.
- `architecture-zone-enforcement`: convention and disabled-test rules cover all test assemblies via `Njord.Architecture.Tests`.

## Impact

- `openspec/specs/test-project-structure/spec.md`, `openspec/specs/architecture-zone-enforcement/spec.md` only.
- API budget: 0 requests/month (docs only, no polling change).

## Non-goals

- No change to test code, project structure, CI or workflows.
- No new requirements; the specs only catch up with the layout that `split-test-projects` already created.
- The later extraction of Mqtt and Enrichment from the host (`extract-enrichment-mqtt-projects`) is not described here.
