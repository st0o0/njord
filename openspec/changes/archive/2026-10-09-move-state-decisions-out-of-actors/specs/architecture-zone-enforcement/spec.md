## ADDED Requirements

### Requirement: Foundation layers have no framework dependency
Types in the foundation layers (`Njord.Weather`, `Njord.Sensors`, `Njord.Messages`,
`Njord.Persistence`) SHALL NOT depend on any `Akka.*` namespace.

#### Scenario: Foundation layer gains an Akka dependency
- **WHEN** a type in `Njord.Weather`, `Njord.Sensors`, `Njord.Messages`, or
  `Njord.Persistence` gains a dependency on any `Akka.*` type
- **THEN** the architecture test run fails and names the offending type and dependency

#### Scenario: Foundation layers currently pass
- **WHEN** the architecture tests run against the current code
- **THEN** they pass, with none of the four foundation projects referencing any `Akka.*`
  package or namespace
