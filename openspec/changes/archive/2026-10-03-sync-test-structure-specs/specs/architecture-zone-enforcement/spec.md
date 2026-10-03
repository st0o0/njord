## MODIFIED Requirements

### Requirement: Test classes are sealed Spec classes
Every test class in every `Njord.*Tests` assembly (the per-library test projects, the host-resident `Njord.Tests` and `Njord.Architecture.Tests` itself) that contains tests SHALL be `sealed` and its name SHALL end in `Spec`.

#### Scenario: Test class violates the convention
- **WHEN** a class containing `[Fact]` or `[Theory]` methods is unsealed or not suffixed `Spec`
- **THEN** the architecture test run fails and lists the class

### Requirement: Convention rules cover all production assemblies
The sealed-class convention SHALL be evaluated over all `Njord.*` production assemblies in one architecture load, and the test-class convention SHALL cover every test project: `Njord.Architecture.Tests` SHALL load every `Njord.*Tests` assembly (and `Njord.Tests.Shared`) next to it.

#### Scenario: New library is covered automatically
- **WHEN** a new `Njord.*` production assembly is added to the solution and referenced by the host
- **THEN** its non-abstract classes are checked by the sealed rule without editing the rule

#### Scenario: New test project is covered automatically
- **WHEN** a new `Njord.<Name>.Tests` project is added next to the existing ones
- **THEN** `Njord.Architecture.Tests` picks it up through its `Njord.*.Tests` project reference pattern and the test-class and disabled-test rules check it without editing the rules

### Requirement: No disabled tests
Test methods in every test assembly (all `Njord.*Tests` projects, including `Njord.Architecture.Tests` itself, and `Njord.Tests.Shared`) SHALL NOT be disabled: no `Fact` or `Theory` attribute SHALL carry a non-empty `Skip` (or `SkipUnless`/`SkipWhen`), and no `Ignore` attribute SHALL be used. The check (`DisabledTestArchitectureSpec` in `Njord.Architecture.Tests`) SHALL cover public and non-public methods of all types, so it holds independent of the file naming that slopwatch's disabled-test rule inspects.

#### Scenario: Skipped test
- **WHEN** a test method declares `[Fact(Skip = "...")]` or `[Theory(Skip = "...")]`
- **THEN** the architecture test run fails and names the declaring type and method

#### Scenario: Ignored test
- **WHEN** a test method or class carries an `Ignore` attribute
- **THEN** the architecture test run fails and names the declaring type and method

#### Scenario: No disabled tests in the current code
- **WHEN** the architecture tests run against the current test assemblies
- **THEN** they pass
