## Context

Facts verified 2026-10-02 (network available):

- `dotnet tool search slopwatch` -> `slopwatch.cmd` 0.4.2 (Aaron Stannard).
  The plugin skill (`dotnet-skills` 1.6.0) shows `"version": "0.2.0"`.
- Commands: `analyze` (default verb), `init`, `list-rules`, `version`.
- With a local-tool manifest entry whose command is `slopwatch`, both
  `dotnet slopwatch` and `dotnet tool run slopwatch` work (SDK-confirmed in a
  throw-away copy).
- Without `.slopwatch/baseline.json`, `analyze` refuses to run and asks for
  `init` or `--no-baseline`.
- `analyze` flags: `-d <dir>` (default cwd), `--fail-on info|warning|error`
  (default `error`), `--no-baseline`, `--create-baseline`, `--update-baseline`,
  `--hook` (git-dirty files only, exit 2), `-o json`, `--stats`, `-c <config>`.
  Default patterns: cs, razor, cshtml, csproj, props, targets.
- Rules: SW001 disabled tests (Error), SW002 warning suppression (Warning),
  SW003 empty catch (Error), SW004 test delays (Warning), SW005 project-file slop
  such as NoWarn (Warning), SW006 CPM bypass (Error in 0.4.2).
- Trial on a copy of the repo (315 files, ~10 s): `--no-baseline --min-severity info`
  reports 0 issues; `init` writes an empty baseline; an injected empty `catch {}`
  gave exit 1 (SW003) and removing it restored exit 0. An injected
  `[Fact(Skip="x")]` was not reported in that probe (to re-check in validation).
  Existing `Task.Delay` uses in `Njord.Tests` were not flagged.

## Decisions

1. **Manifest** at `.config/dotnet-tools.json`, `isRoot: true`, version pinned to
   `0.4.2`, `rollForward: false`. Renovate tracking of the manifest is verified
   in tasks (config extends a shared preset we cannot inspect here).
2. **Baseline strategy:** commit `.slopwatch/baseline.json` from `init` (empty
   today). Nothing to fix first. New findings fail `analyze` (exit 1 on error
   severity). Strict mode (`--fail-on warning`) is the documented command so
   warnings (SW002/4/5) also block. `--update-baseline` only with a justification.
3. **Invocation:** always from the repo root: `dotnet tool restore` once, then
   `dotnet slopwatch analyze -d .`. Note `AGENTS.md` build commands run from
   `src/`; slopwatch is the exception.
4. **Docs:** replace the vague mentions in `AGENTS.md` and `CLAUDE.md` with the
   exact commands; keep the skill route `dotnet-skills:slopwatch` but note that
   the skill's example version (0.2.0) is outdated and the repo manifest wins.
5. **CI:** not part of this change. Wiring slopwatch into `.github/workflows/`
   is deferred and must be discussed with the user before any design is applied.

## Risks

- Tool is third-party and young (0.x); pinning plus Renovate PRs bound the risk.
- Empty baseline means any future legitimate suppression needs a baseline update
  or `[SlopwatchSuppress("SWxxx", "reason")]` with a 20+ char reason.
- Skip detection gap noted above; verify, and report upstream if confirmed.
