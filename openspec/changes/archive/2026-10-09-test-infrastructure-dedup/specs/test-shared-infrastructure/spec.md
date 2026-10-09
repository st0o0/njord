## ADDED Requirements

### Requirement: Actor specs share a base class for common setup
Actor test specs (`*Spec` classes that extend `Akka.Hosting.TestKit.TestKit`) SHALL extend
a shared `NjordActorSpec` base class in `Njord.Tests.Shared` that provides default
`ConfigureServices`/`ConfigureAkka` setup (test persistence, timefactor, default options),
unless the spec needs fundamentally different configuration.

#### Scenario: New actor spec uses base class
- **WHEN** a new actor spec is created
- **THEN** it extends `NjordActorSpec` and overrides only the spec-specific configuration,
  not the common persistence/timefactor/options setup

### Requirement: Time constants and FakeTimeProvider are shared
Test files SHALL use shared `TestTimeConstants` epoch values and a
`FakeTimeProvider` factory method from `Njord.Tests.Shared` instead of inline
`new DateTimeOffset(...)` / `new FakeTimeProvider(...)` calls.

#### Scenario: No hardcoded DateTimeOffset duplicates across test projects
- **WHEN** test files are inspected for `new DateTimeOffset(2026,` patterns
- **THEN** each distinct epoch value appears only once (in `TestTimeConstants`), and all
  other occurrences reference the shared constant
