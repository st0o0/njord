## Context

njord has 5 GitHub Actions workflows: `ci.yml` (PR validation — unit tests, hadolint, Docker build + smoke test), `commitlint.yml` (Conventional Commits check), `dev-build.yml` (label-gated dev images), `release.yml` (release-please + multi-arch Docker + cosign), and `security.yml` (Trivy container scan + weekly cron). All work correctly but have room for efficiency improvements and better coverage.

Current state of the workflows reviewed in the explore session:

- No NuGet package caching — every `dotnet restore` downloads from scratch.
- No code formatting gate — style drift is unchecked.
- `commitlint.yml` is a standalone workflow for a single job — adds required-check management overhead.
- Trivy scans the container image but `dotnet list package --vulnerable` (NuGet-level audit) is not run.
- `release.yml` trusts PR-CI without its own test run — a merge race could release broken code.
- Dependabot creates individual PRs for each package, even tightly coupled ones like Akka.*.

## Goals / Non-Goals

**Goals:**
- Faster PR feedback (NuGet caching)
- Catch style drift automatically (dotnet format)
- Fewer workflows to maintain (merge commitlint)
- NuGet-level vulnerability detection (dotnet list package --vulnerable)
- Release safety-net (test before Docker build)
- Fewer Dependabot PRs for related packages (grouped updates)

**Non-Goals:**
- Not adding integration tests, E2E tests, or performance tests
- Not changing the release-please, cosign, or SBOM pipeline logic
- Not modifying dev-build.yml (it works well as-is with the environment gate)
- No application code changes

## Decisions

### 1. NuGet cache strategy

**Decision:** Use `actions/cache` with key `nuget-${{ runner.os }}-${{ hashFiles('src/**/*.csproj', 'src/Directory.Packages.props') }}` and path `~/.nuget/packages`. Add to the `unit` and `build` jobs in `ci.yml`, and to the new `test` job in `release.yml`.

**Alternative considered:** `actions/setup-dotnet` has built-in caching via `cache: true`, but it requires a lockfile (`packages.lock.json`) which this project doesn't generate. The manual `actions/cache` approach works with the props/csproj hash instead.

### 2. dotnet format placement

**Decision:** Add `dotnet format --verify-no-changes` as a new step in the existing `lint` job in `ci.yml`, after hadolint. Both are fast checks that don't need the Docker build context.

**Alternative considered:** Separate `format` job for parallelism. Rejected because `dotnet format` on a small codebase takes <10s — the job startup overhead would exceed the check itself. Sharing the `lint` job avoids a redundant `actions/checkout` + `actions/setup-dotnet`.

### 3. Commitlint merge approach

**Decision:** Add a `commitlint` job to `ci.yml` and delete `commitlint.yml`. The job uses the same `wagoid/commitlint-github-action@v6` action with `fetch-depth: 0`.

**Impact on branch protection:** The required check name changes from `commitlint / commitlint` to `ci / commitlint`. This needs a one-time update in the repo's branch protection settings after merging.

**Alternative considered:** Keep separate for isolation. Rejected because the single-step workflow adds no isolation benefit — a failure in commitlint doesn't affect the other CI jobs since they run in parallel anyway.

### 4. Package vulnerability audit design

**Decision:** Add a `nuget-audit` job to `security.yml` that runs `dotnet restore` then `dotnet list package --vulnerable --include-transitive` on the solution. Fail the job if vulnerabilities are found (exit code from `--vulnerable` is non-zero when advisories exist). This complements Trivy's container-level scan.

**Why both Trivy and nuget-audit:** Trivy scans the runtime container image (OS packages, .NET runtime, published app). `dotnet list package --vulnerable` catches NuGet advisories that may not manifest as CVEs in the container scan (e.g., logic bugs, data handling issues). They cover different layers.

### 5. Release test safety-net

**Decision:** Add a `test` job to `release.yml` that runs after `release-please` (only when `release_created == 'true'`) and before `docker`. The `docker` job gets `needs: [release-please, test]`.

The test job is identical to the `unit` job in `ci.yml`: checkout, setup-dotnet, `dotnet run --project Njord.Tests/Njord.Tests.csproj`.

**Alternative considered:** Trust PR-CI exclusively. Rejected because a merge race (PR A passes CI, PR B passes CI, both merge, the combination breaks) is a real scenario in active repos. The test job adds ~1 min to releases but only runs when release-please creates a release.

### 6. Dependabot grouping

**Decision:** Add `groups` to the `nuget` ecosystem in `dependabot.yml`:

- `akka`: patterns `Akka.*`, `Akka.Hosting*`, `Akka.Persistence*`, `Akka.Streams*`
- `testing`: patterns `xunit.*`, `Microsoft.Testing.*`, `Microsoft.AspNetCore.Mvc.Testing`, `Microsoft.Extensions.TimeProvider.Testing`, `Verify.*`
- `servus`: patterns `Servus`, `Servus.*`

Ungrouped packages (MQTTnet, Testcontainers, OpenTelemetry.Api, SQLitePCLRaw.*) get individual PRs as before — they're independent and shouldn't be blindly bundled.

## Risks / Trade-offs

- **[NuGet cache staleness]** → The cache key includes the hash of all csproj + Directory.Packages.props files. A package version bump always invalidates the cache. Stale cache = cold restore (same as today). No downside.
- **[dotnet format false positives]** → If the codebase has existing formatting inconsistencies, the first PR after this change will fail format check. Mitigation: run `dotnet format` once locally and commit the result before or alongside this change.
- **[Branch protection update required]** → Merging the commitlint job into ci.yml changes the required check name. If branch protection references the old name, PRs will be stuck. Mitigation: update branch protection settings immediately after merging.
- **[Release test adds latency]** → ~1 min to release pipeline. Acceptable: releases are infrequent and the safety-net is worth the wait.

## Migration Plan

1. Merge the PR with all workflow changes.
2. Update branch protection required checks: remove `commitlint / commitlint`, ensure `ci / commitlint` (or just `ci / unit`, `ci / lint`, `ci / build`) are required.
3. Delete any stale `commitlint.yml` workflow runs from the Actions tab.
4. Verify a subsequent PR triggers all expected jobs.
