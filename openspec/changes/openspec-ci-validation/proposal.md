## Why

**Status: blocked — design to be discussed with the user.** No CI or workflow file may be created or changed before the design below is agreed (project policy: discuss CI designs first).

Nothing in CI checks OpenSpec artifacts or where they live. The local checks (`openspec validate --all`, root guard script) are introduced by `openspec-hygiene`; this change is about running them automatically on pull requests. The earlier design (separate `.github/workflows/openspec.yml`, npm-installed `@fission-ai/openspec@1.7.0`, blocking `validate --all`, guard in CI) was rejected by the user without an alternative, so this is a proposal only.

## What Changes

- Decide the CI design (see design.md, Open questions), then add the agreed workflow/job.
- Nothing is implemented until the decisions are recorded in design.md.

## Capabilities

### New Capabilities

None. CI tooling only; `skip_specs: true`.

### Modified Capabilities

None.

## Impact

- Depends on `openspec-hygiene` (green `openspec validate --all`, local guard script), otherwise a blocking CI check starts red.
- FunkArr (`/home/st0o0/GIT/FunkArr`) already has a local commit `aa6952f` with one design (separate `openspec.yml`, npm-pinned CLI, guard script under `.github/scripts/`, report-only specs step). It is not touched here; the choice must be made consistently across both repos.
- API budget: none, no polling added or altered (0 requests/month).
- No code under `src/Njord/` or `src/Njord.Tests/`.

## Non-goals

- Implementing anything before the design is agreed.
- Editing FunkArr.
- Spec repairs, root merge, local tooling (those are in `openspec-hygiene`).
