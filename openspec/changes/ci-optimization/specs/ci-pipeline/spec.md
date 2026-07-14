## ADDED Requirements

### Requirement: NuGet package cache in CI

PR validation workflows SHALL cache NuGet packages between runs to avoid redundant restores.

#### Scenario: Cache hit on unchanged dependencies
- **WHEN** a PR CI run starts and the csproj/Directory.Packages.props files have not changed since the last run
- **THEN** NuGet packages are restored from cache without downloading from nuget.org

#### Scenario: Cache miss on dependency change
- **WHEN** a package version in Directory.Packages.props or a csproj file changes
- **THEN** the cache key invalidates and packages are restored from nuget.org

### Requirement: Code format gate on PRs

The CI pipeline SHALL reject PRs with code formatting inconsistencies.

#### Scenario: Formatting violation
- **WHEN** a PR contains C# files that differ from `dotnet format` output
- **THEN** the lint job fails with a diff showing the formatting violations

#### Scenario: Clean formatting
- **WHEN** all C# files in a PR match `dotnet format` output
- **THEN** the lint job passes the format check

### Requirement: Commitlint as CI job

Conventional Commit validation SHALL run as a job within `ci.yml`, not as a standalone workflow.

#### Scenario: Non-conventional commit message
- **WHEN** a PR contains a commit that does not follow Conventional Commits
- **THEN** the `commitlint` job in the `ci` workflow fails

#### Scenario: Standalone commitlint workflow removed
- **WHEN** a contributor inspects `.github/workflows/`
- **THEN** there is no `commitlint.yml` file

### Requirement: NuGet vulnerability audit

The security workflow SHALL audit NuGet packages for known vulnerabilities at the package level, in addition to the container-level Trivy scan.

#### Scenario: Vulnerable transitive dependency
- **WHEN** a NuGet package or its transitive dependency has a known vulnerability advisory
- **THEN** the `nuget-audit` job in the security workflow reports it

### Requirement: Release test safety-net

The release workflow SHALL run the test suite before building the Docker image, to guard against merge race conditions.

#### Scenario: Tests pass before release build
- **WHEN** release-please creates a release
- **THEN** the test suite runs and passes before the Docker build job starts

#### Scenario: Tests fail before release build
- **WHEN** release-please creates a release but the test suite fails
- **THEN** the Docker build job does not run

### Requirement: Dependabot grouped updates

Dependabot SHALL group related NuGet packages into single PRs.

#### Scenario: Akka packages updated together
- **WHEN** new versions are available for multiple Akka.* packages
- **THEN** Dependabot creates a single PR updating all of them together

#### Scenario: Test packages updated together
- **WHEN** new versions are available for xunit.* and Microsoft.Testing.* packages
- **THEN** Dependabot creates a single PR updating all of them together
