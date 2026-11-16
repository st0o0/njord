# test-shared-infrastructure Specification

## Purpose

Shared test infrastructure in `Njord.Tests.Shared`: shared time constants and a `FakeTimeProvider` factory.

## Requirements
### Requirement: Actor tests extend the Hosting TestKit directly
Actor test classes (`*Tests` classes) SHALL extend `Akka.Hosting.TestKit.TestKit` directly and
use shared helpers from `Njord.Tests.Shared` (test persistence, timefactor, default options)
in their `ConfigureAkka`/`ConfigureServices` overrides; there SHALL be no shared abstract actor test base class.

#### Scenario: New actor test uses the TestKit directly
- **WHEN** a new actor test class is created
- **THEN** it extends `Akka.Hosting.TestKit.TestKit` and wires persistence, timefactor and options
  through the shared helpers

### Requirement: Time constants and FakeTimeProvider are shared
Test files SHALL use shared `TestTimeConstants` epoch values and a
`FakeTimeProvider` factory method from `Njord.Tests.Shared` instead of inline
`new DateTimeOffset(...)` / `new FakeTimeProvider(...)` calls.

#### Scenario: No hardcoded DateTimeOffset duplicates across test projects
- **WHEN** test files are inspected for `new DateTimeOffset(2026,` patterns
- **THEN** each distinct epoch value appears only once (in `TestTimeConstants`), and all
  other occurrences reference the shared constant

