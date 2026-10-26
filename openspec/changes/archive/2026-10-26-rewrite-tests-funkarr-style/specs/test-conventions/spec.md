## MODIFIED Requirements

### Requirement: Test source conventions are machine-checked
`Njord.Architecture.Tests` SHALL scan the source files of every test project and fail when an async
`[Fact]`/`[Theory]` has no `Timeout`, when `Thread.Sleep` or `Task.Delay` with a literal duration is
used, when `DateTime.UtcNow`, `DateTimeOffset.UtcNow` or `DateTime.Now` appears, when an options
type with shipped defaults is constructed empty with `new XOptions()` instead of
`AppSettingsDefaults`, when `Assert.True`/`Assert.False` wraps an equality comparison, or when the
null-forgiving operator is used on a non-`JsonNode` receiver. Allow-list entries SHALL carry a reason
and SHALL fail the spec when they are no longer needed. Test classes SHALL end in `Tests`, not `Spec`.
Test classes that test actors SHALL inherit `Akka.TestKit.Xunit.TestKit` directly, not a shared base
class. No test file SHALL reference `TestTimeouts` or `AddTestTimefactor`.

#### Scenario: Missing timeout
- **WHEN** a test declares `[Fact] public async Task X()` without `Timeout`
- **THEN** the architecture test fails and names the file and method

#### Scenario: Arbitrary delay
- **WHEN** a test calls `await Task.Delay(100)`
- **THEN** the architecture test fails

#### Scenario: Stale allow-list entry
- **WHEN** an allow-listed location no longer violates the rule
- **THEN** the architecture test fails until the entry is removed

#### Scenario: The scanner detects offenders
- **WHEN** the scanner is run on a synthetic snippet with each offending pattern
- **THEN** every pattern is reported (negative self-test)

#### Scenario: Spec suffix violation
- **WHEN** a test class ends in `Spec` instead of `Tests`
- **THEN** the architecture test fails

#### Scenario: Shared base class violation
- **WHEN** a test class inherits from `NjordActorSpec` or any abstract `*SpecBase`/`*TestKit` class
- **THEN** the architecture test fails

#### Scenario: Timeout constant violation
- **WHEN** a test file references `TestTimeouts` or calls `AddTestTimefactor`
- **THEN** the architecture test fails

## REMOVED Requirements

### Requirement: Documentation references are verified
**Reason**: Removed during test convention migration. Test count and helper name verification adds
maintenance cost that exceeds its value during a mass rename.
**Migration**: Remove the architecture test that checks AGENTS.md references and test counts.
