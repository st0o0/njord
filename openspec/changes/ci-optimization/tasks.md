## 1. NuGet caching in ci.yml

- [x] 1.1 Add `actions/cache` step before `dotnet restore` in the `unit` job with path `~/.nuget/packages` and key `nuget-${{ runner.os }}-${{ hashFiles('src/**/*.csproj', 'src/Directory.Packages.props') }}` with a `restore-keys` fallback of `nuget-${{ runner.os }}-`.
- [x] 1.2 Add the same cache step to the `build` job in `.github/workflows/ci.yml`.

## 2. Code format check in ci.yml

- [x] 2.1 Run `dotnet format src/Njord.slnx --verify-no-changes` locally and commit any formatting fixes as a prerequisite.
- [x] 2.2 Add `actions/setup-dotnet` and a `dotnet format src/Njord.slnx --verify-no-changes` step to the `lint` job in `.github/workflows/ci.yml`.

## 3. Merge commitlint into ci.yml

- [x] 3.1 Add a `commitlint` job to `.github/workflows/ci.yml` using `actions/checkout@v4` with `fetch-depth: 0` and `wagoid/commitlint-github-action@v6`.
- [x] 3.2 Delete `.github/workflows/commitlint.yml`.

## 4. NuGet vulnerability audit in security.yml

- [x] 4.1 Add a `nuget-audit` job to `.github/workflows/security.yml` that runs `actions/checkout`, `actions/setup-dotnet`, `dotnet restore src/Njord.slnx`, and `dotnet list src/Njord.slnx package --vulnerable --include-transitive`. Fail on non-zero exit.
- [x] 4.2 Add the same `on.pull_request.paths` triggers as the existing Trivy job so it runs on csproj/props changes.

## 5. Release test safety-net

- [x] 5.1 Add a `test` job to `.github/workflows/release.yml` that runs after `release-please` (condition: `release_created == 'true'`), checks out the code, sets up .NET SDK, and runs `dotnet run --project Njord.Tests/Njord.Tests.csproj` from `src/`.
- [x] 5.2 Add NuGet caching to the `test` job (same cache key as ci.yml).
- [x] 5.3 Update the `docker` job's `needs` from `[release-please]` to `[release-please, test]`.

## 6. Dependabot grouped updates

- [x] 6.1 Add `groups` to the `nuget` ecosystem in `.github/dependabot.yml`: `akka` (patterns: `Akka*`, `Akka.Hosting*`, `Akka.Persistence*`, `Akka.Streams*`), `testing` (patterns: `xunit*`, `Microsoft.Testing*`, `Microsoft.AspNetCore.Mvc.Testing`, `Microsoft.Extensions.TimeProvider.Testing`, `Verify*`), `servus` (patterns: `Servus`, `Servus.*`).

## 7. Validation

- [x] 7.1 Review final `.github/workflows/ci.yml` for correct job dependencies and step ordering.
- [x] 7.2 Review final `.github/workflows/security.yml` for correct trigger paths and job structure.
- [x] 7.3 Review final `.github/workflows/release.yml` for correct `needs` chain: `release-please → test → docker`.
- [x] 7.4 Verify `.github/workflows/commitlint.yml` no longer exists.
- [x] 7.5 Verify `.github/dependabot.yml` has the expected group definitions.

## Post-merge (manual)

> After merging the PR, update branch protection required checks:
> remove `commitlint / commitlint`, add `ci / commitlint` if it should be required.
