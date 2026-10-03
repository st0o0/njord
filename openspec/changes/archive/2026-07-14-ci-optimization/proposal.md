## Why

The CI workflows work correctly but leave performance and coverage on the table: every run restores NuGet from scratch (~10-20s), code style has no automated gate, commitlint lives in its own workflow adding required-check overhead, NuGet-level vulnerability detection is missing (Trivy only scans the container image), the release pipeline trusts PR-CI without its own test run, and Dependabot creates one PR per package even for tightly coupled groups like Akka.*.

## What Changes

- **NuGet caching** — add `actions/cache` for `~/.nuget/packages` in `ci.yml` (unit and build jobs) to avoid redundant restores.
- **Code format check** — add `dotnet format --verify-no-changes` as a step in `ci.yml` to enforce consistent code style on PRs.
- **Merge commitlint** — fold the single-step `commitlint.yml` into `ci.yml` as a `commitlint` job, delete the standalone workflow. Simplifies required-checks configuration (one workflow, multiple jobs).
- **Package vulnerability audit** — add a `nuget-audit` job to `security.yml` running `dotnet list package --vulnerable` alongside the existing Trivy container scan.
- **Release test safety-net** — add a test job in `release.yml` that the Docker build job depends on, guarding against merge race conditions where main receives a broken commit between PR-CI and release.
- **Dependabot grouped updates** — configure `groups` in `dependabot.yml` to bundle related NuGet packages (Akka.*, Microsoft.Testing.*, xunit.*) into single PRs.

## Non-goals

- Not changing what CI validates (same tests, same lint rules) — only where and how efficiently.
- Not adding new test types (integration tests, E2E, performance).
- Not changing the release-please or cosign/SBOM pipeline.
- No application code changes.

## Capabilities

### New Capabilities

_(none — CI infrastructure changes, no new application capabilities)_

### Modified Capabilities

_(none — no spec-level behavior changes)_

## Impact

- **Files changed**: `.github/workflows/ci.yml`, `.github/workflows/security.yml`, `.github/workflows/release.yml`, `.github/dependabot.yml`. Deletion of `.github/workflows/commitlint.yml`.
- **CI speed**: ~10-20s faster per PR run (NuGet cache hit).
- **Required checks**: `commitlint` job moves from its own workflow into `ci.yml` — branch protection rules need updating to reference `ci / commitlint` instead of `commitlint / commitlint`.
- **Dependabot PR volume**: fewer PRs for grouped packages (e.g. 1 Akka PR instead of 3).
