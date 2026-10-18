# test-conventions Specification

## Purpose
TBD - created by archiving change test-rules-enforcement. Update Purpose after archive.
## Requirements
### Requirement: Test source conventions are machine-checked
`Njord.Architecture.Tests` SHALL scan the spec files of every test project and fail when an async `[Fact]`/`[Theory]` has no `Timeout`, when `Thread.Sleep` or `Task.Delay` with a literal duration is used, when `DateTime.UtcNow`, `DateTimeOffset.UtcNow` or `DateTime.Now` appears, when an options type with shipped defaults is constructed empty with `new XOptions()` instead of `AppSettingsDefaults`, when `Assert.True`/`Assert.False` wraps an equality comparison, or when the null-forgiving operator is used on a non-`JsonNode` receiver. Allow-list entries SHALL carry a reason and SHALL fail the spec when they are no longer needed.

#### Scenario: Missing timeout
- **WHEN** a spec declares `[Fact] public async Task X()` without `Timeout`
- **THEN** the architecture spec fails and names the file and method

#### Scenario: Arbitrary delay
- **WHEN** a spec calls `await Task.Delay(100)`
- **THEN** the architecture spec fails

#### Scenario: Stale allow-list entry
- **WHEN** an allow-listed location no longer violates the rule
- **THEN** the architecture spec fails until the entry is removed

#### Scenario: The scanner detects offenders
- **WHEN** the scanner is run on a synthetic snippet with each offending pattern
- **THEN** every pattern is reported (negative self-test)

### Requirement: Documentation references are verified
A spec SHALL verify that test helper names, spec file names and projects mentioned in `AGENTS.md` and in `.claude/skills/*/SKILL.md` exist, and that the total test count stated in `AGENTS.md` equals the number of tests discovered across the test assemblies.

#### Scenario: Reference to a missing helper
- **WHEN** `AGENTS.md` mentions `AddFastRetryBackoff` and no such symbol exists
- **THEN** the spec fails

#### Scenario: Count drift
- **WHEN** a spec is added and the documented total is not updated
- **THEN** the spec fails and prints the discovered total

### Requirement: Machine-checked and review-only rules are separated
`AGENTS.md` SHALL state which test conventions are machine-checked and which remain review rules.

#### Scenario: Reader checks enforcement
- **WHEN** a contributor reads the test conventions
- **THEN** each rule is labelled as checked by a named spec or as a review rule

