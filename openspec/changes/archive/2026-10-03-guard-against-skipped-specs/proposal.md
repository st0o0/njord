## Why

The pinned slopwatch 0.4.2 rule SW001 (disabled tests) only inspects files named
`*Tests.cs`. This repo names its test files `*Spec.cs`, so `[Fact(Skip = ...)]`,
`[Theory(Skip = ...)]` and `[Ignore]` go unreported (documented in `AGENTS.md`).
A silently disabled spec is exactly the shortcut the quality gate should catch.

## What Changes

- Add an architecture test in `src/Njord.Tests/Architecture` that reflects over all
  methods of all types in the test assemblies (`Njord.Tests`, `Njord.Tests.Shared`)
  and fails on any non-empty `Skip` (plus `SkipUnless`/`SkipWhen` when present) on a
  test attribute, or on any `Ignore`-named attribute.
- Update the slopwatch known-limits wording in `AGENTS.md` and `CLAUDE.md`.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `architecture-zone-enforcement`: ADDED requirement "No disabled tests".

## Non-goals

- No change to slopwatch itself, its config or baseline; no upstream report.
- No CI wiring.
- No production code changes.

## Impact

- New spec file in `src/Njord.Tests/Architecture`; edited docs `AGENTS.md`, `CLAUDE.md`.
- API budget: 0 Open-Meteo requests/month.
