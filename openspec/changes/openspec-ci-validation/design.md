## Context

**Status: blocked — design to be discussed with the user.** Nothing below is a decision.

Facts: `.github/workflows/ci.yml` only calls reusable workflows from `st0o0/github-workflows@main`; `commitlint.yml` there uses plain `actions/checkout@v7` tags. The OpenSpec CLI is `@fission-ai/openspec` (local 1.7.0; a global `openspec@0.0.0` also exists on the dev machine). njord needs no Node toolchain otherwise. Local checks (guard script, `openspec validate --all`) come from `openspec-hygiene`.

FunkArr commit `aa6952f` (local, unpushed) implements one design: separate `openspec.yml`, `npm install -g` of a pinned CLI, guard script in `.github/scripts/`, spec validation report-only (17 of 170 specs fail there). Whatever is chosen must be applied consistently to both repos.

## Open questions

### Q1. Where does the check live?
- A. Separate workflow `.github/workflows/openspec.yml` (FunkArr's choice). + isolated, no `paths` filter needed, own timeout. - a second workflow file per repo, duplicated.
- B. Extra job in `ci.yml`. + one place, one concurrency group. - `ci.yml` is currently only reusable-workflow calls.
- C. Reusable workflow in `st0o0/github-workflows`, called from `ci.yml`. + one definition for both repos, pin bumped once. - outward-facing change in another repo, `@main` coupling.

### Q2. How is the CLI installed, and pinned?
- A. `npm install -g @fission-ai/openspec@<ver>` after `actions/setup-node`. + simple, explicit. - global install, needs a Node version pin.
- B. `npx --yes @fission-ai/openspec@<ver> validate ...`. + no global state. - re-downloads each run, same Node need.
- C. Other (committed `package.json`/lockfile, container, cached binary). + reproducible with lockfile integrity. - adds Node tooling to a .NET repo.
- Pin: exact version (matches local CLI, bump deliberately) vs range/latest (no maintenance, can break CI). Renovate can manage an exact pin only if the version sits in a recognizable place.

### Q3. Blocking or report-only?
- Blocking: invalid specs cannot merge; requires green `--all` first (done by `openspec-hygiene` for njord; not for FunkArr).
- Report-only (`continue-on-error`): never red, but easily ignored.
- Mixed: guard blocking, validation report-only.

### Q4. Which validation scope?
- `openspec validate --all`: changes and specs, strictest.
- `--changes` only: guards new work, ignores legacy spec debt.
- `--specs` only: the reverse.
- Run with `--no-interactive` in every case.

### Q5. Root guard in CI?
- Run `scripts/check-openspec-root.sh` (local script from `openspec-hygiene`) in CI: catches a second `openspec/` root early; no `paths` filter, since a stray root can appear anywhere.
- Skip in CI: relies on local discipline, nothing stops a regression.
- Script location: `scripts/` (local, non-CI) vs `.github/scripts/` (FunkArr's choice) — keep the same path in both repos.

### Q6. Action pinning style
- Tags (`actions/checkout@v7`, as in `commitlint.yml`; Renovate bumps them) vs full commit SHAs (supply-chain hardening, noisier) vs mixed. `permissions: contents: read` and `concurrency` in every case.

## Decisions

None yet. Record each answer here, with the FunkArr outcome, before any task runs.

## Risks / Trade-offs

- Blocking check on a repo with spec debt starts red (mitigated in njord by `openspec-hygiene`).
- Divergent designs between njord and FunkArr double maintenance.
- A global npm install in CI adds a supply-chain surface (mitigated by exact pin, `contents: read`, no secrets).

## Migration Plan

After decisions: implement in njord, align FunkArr (amend or replace `aa6952f`, its owner decides), optionally extract to the shared workflows repo. Rollback: revert the commit.
