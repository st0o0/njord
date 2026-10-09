## Context

`Njord.Domain` currently holds two unrelated bounded contexts side by side
(`Weather/`, `Sensors/`) under a generic name that also collides with the
"Domain" *zone* concept in AGENTS.md's architecture guardrails (Ingest →
Domain → Egress). The feature library `Njord.Sensors` (actor + gRPC ingestion)
already names its actor, marker interface, and registered actor path
`SensorHubActor` / `ISensorHubActor` / `"sensor-hub"` — the assembly name is
the only thing still generic.

This is a cross-cutting rename: ~137 files reference the old namespaces,
touching every feature library, the architecture tests, the `.slnx`, and
`AGENTS.md`. No runtime behavior, message contract, or wire format changes.

## Goals / Non-Goals

**Goals:**
- Every foundation/feature library name signals its bounded context.
- `Njord.Core` and `Njord.Compute` names are untouched (confirmed fine).
- Zero behavior change — build and all 817 tests pass identically before and
  after, just under new assembly/namespace names.
- `Njord.Architecture.Tests` keeps enforcing the same reference-direction
  rules, just with updated names.

**Non-Goals:**
- No change to message contracts, persistence DTOs, MQTT payloads, or gRPC
  protos.
- No change to the conceptual "Domain" zone name in AGENTS.md's guardrails
  prose — only the literal `Njord.Domain` project disappears.
- No fix for the pre-existing `Njord.Domain.Analysis` vs.
  `Njord.Compute.Analysis` spec drift (out of scope, predates this change).

## Decisions

- **Split `Njord.Domain` into `Njord.Weather` + `Njord.Sensors` (foundation),
  not into `Njord.Domain.Weather`/`Njord.Domain.Sensors` as two nested
  namespaces of one assembly.** Separate assemblies make the bounded-context
  cut visible in the solution explorer and in `ProjectReference`s, not just in
  folder names. Alternative considered: keep one assembly, rename only the
  root namespace — rejected because it doesn't give feature libraries the
  option to depend on only one of the two contexts (e.g. `Njord.Ingest`
  genuinely only needs `Njord.Weather`, never sensor types).
- **Rename the feature library `Njord.Sensors` → `Njord.SensorHub` instead of
  inventing a new name.** The actor, marker interface, and registered actor
  name already say "SensorHub" everywhere; renaming the assembly to match is
  the lowest-friction option and removes the only remaining name mismatch.
- **Namespace per project matches the assembly name exactly**
  (`Njord.Weather`, `Njord.Sensors`, `Njord.SensorHub`) — no extra nesting
  segment, consistent with how `Njord.Core`, `Njord.Compute`, etc. already
  work (assembly name == root namespace).
- **Test project split mirrors the production split.**
  `Njord.Domain.Tests/Weather/*` → `Njord.Weather.Tests`,
  `Njord.Domain.Tests/Sensors/*` → `Njord.Sensors.Tests`. The existing
  `Njord.Sensors.Tests` (actor specs, e.g. `SensorHubActorSpec.cs`) is
  renamed to `Njord.SensorHub.Tests` first (or as part of the same step) to
  free the `Njord.Sensors.Tests` name for the new foundation test project —
  order matters to avoid a transient name collision.
- **Relocate `Options/LocationOptions.cs` to `Njord.Core/Configuration/`
  while splitting `Njord.Domain`, instead of putting it in `Njord.Weather`.**
  It's a config POCO (bound from `appsettings`), not a weather value object,
  and the pre-existing `domain-core-merge` spec already required all
  `Domain/Options/*` types to live in `Njord.Core/Configuration` — this was
  simply never done. Splitting `Domain` forces a decision for this file
  anyway, so this change finally conforms to that existing requirement
  rather than carrying the drift into a third project.
- **Do one mechanical pass per project, not one giant diff.** Safer order:
  (1) rename feature lib `Njord.Sensors` → `Njord.SensorHub` (frees the name,
  smaller blast radius — only actor/gRPC consumers), (2) split
  `Njord.Domain` → `Njord.Weather` + `Njord.Sensors`, (3) fix up
  `Njord.Architecture.Tests` and `AGENTS.md` last, once the new assemblies
  exist and build.

## Risks / Trade-offs

- [Large mechanical diff (~137 files) risks a missed reference causing a
  build break] → do the rename with project-wide find/replace on the
  `using`/namespace strings per step, then `dotnet build Njord.slnx` after
  each of the three steps in the Decisions order, not once at the end.
- [`Njord.Architecture.Tests` hardcodes assembly names in
  `NjordArchitecture.BaseLibraryNames` and the zone-check lists] → update
  these in the same change, since a stale list would either false-pass (if it
  silently drops the renamed assembly) or false-fail (if it still expects the
  old name) rather than catching real violations.
- [Git history/blame continuity for moved files] → use `git mv` for the
  directory moves where the file content is unchanged, so history follows the
  file instead of showing as delete+add.

## Migration Plan

Single-PR mechanical rename, no staged rollout (internal structure only, no
external contract changes, no runtime deployment behavior):
1. Rename `Njord.Sensors` (feature lib) → `Njord.SensorHub` (project, csproj,
   namespace, test project); build + run `Njord.SensorHub.Tests`.
2. Split `Njord.Domain` → `Njord.Weather` + `Njord.Sensors` (new foundation
   project), including `Njord.Domain.Tests` → `Njord.Weather.Tests` +
   `Njord.Sensors.Tests`; update every consuming `.csproj` and `using`;
   build + run both new test projects.
3. Update `Njord.slnx`.
4. Update `Njord.Architecture.Tests` (`LayerReferenceSpec`,
   `ZoneArchitectureSpec`, `BaseLibraryNames`) and `AGENTS.md`'s
   solution-structure section.
5. Full solution build + full test loop (all `Njord.*Tests` projects) +
   `dotnet slopwatch analyze -d . --fail-on warning` from repo root.

Rollback: revert the commit(s) — no data migration, no persisted state format
changes, so a plain revert is safe at any point.

## Open Questions

None — scope and naming were confirmed in exploration before this proposal.
