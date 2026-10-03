## Why

`CLAUDE.md` mixes tool-agnostic project rules (architecture, decisions, API facts, conventions) with Claude-specific wiring (OpenSpec slash commands, skill routing). Other agents and tools cannot consume it cleanly, `openspec/config.yaml` duplicates and has drifted from it, and the skill routing references five `sepp:*` skills that do not exist. FunkArr already solved this with an `AGENTS.md` + slim `CLAUDE.md` split; njord should adopt it and pick up the FunkArr conventions that actually fit.

## What Changes

- Add `AGENTS.md` (repo root) holding all tool-agnostic content moved from `CLAUDE.md`: project, architecture guardrails, decisions, build & test, Open-Meteo facts, conventions, references.
- New `AGENTS.md` sections adopted from FunkArr and adapted to njord:
  - Language: English only for code, specs, docs, communication.
  - Solution structure tree (`Njord`, `Njord.Tests`, `Njord.Tests.Shared` and the folder zones inside `Njord`).
  - C# conventions: point to `src/.editorconfig` for enforced style; state only the non-enforceable rules (no `async void`, no `.Result`/`.Wait()`, pass `CancellationToken`).
  - Akka conventions (see design: stated as target rules with a "Known deviations" list until the follow-up migration lands).
  - Test assertion conventions (no `!.`, `Assert.Single`, count guard before indexing).
  - Metrics conventions derived from `Diagnostics/NjordMetrics.cs`.
- Reduce `CLAUDE.md` to `@AGENTS.md`, the OpenSpec workflow and skill routing.
- **Remove** the five dead routes `sepp:actor-pattern-library`, `sepp:resilience-patterns`, `sepp:message-driven-designer`, `sepp:domain-modeling-patterns`, `sepp:complexity-guardian` from skill routing.
- Slim `openspec/config.yaml` `context:` to a short pointer plus essentials; keep `rules:` untouched; remove the unverified "energy" feature from the pipeline description.
- Assess the migration of the Akka-convention deviations (`Status.Failure`, `ContinueWith`) and record the result in `design.md`; the migration itself is a separate follow-up change.

## Capabilities

### New Capabilities

None. This change is documentation and tooling only; no runtime behavior changes.

### Modified Capabilities

None. The change sets `skip_specs: true`.

## Impact

- Files: `AGENTS.md` (new), `CLAUDE.md`, `openspec/config.yaml`. No code under `src/` changes.
- API budget: none. No polling is added or altered (0 additional requests/month against the 300k free-tier limit).
- Risk: agents that previously read only `CLAUDE.md` now rely on the `@AGENTS.md` import; tools without import support must read `AGENTS.md` directly (which is the point of the split).

## Non-goals

- Migrating `Status.Failure` / `ContinueWith` call sites (separate follow-up change).
- Creating njord project skills (`njord-persistent-actor`, `njord-enrichment-feature`, `njord-actor-spec`); separate later change.
- Adding ArchUnit or other architecture tests for the zone rule.
- Adding `dotnet format --verify-no-changes` to CI.
- Changing the content of decisions, API facts or budget guardrails; they move verbatim.
