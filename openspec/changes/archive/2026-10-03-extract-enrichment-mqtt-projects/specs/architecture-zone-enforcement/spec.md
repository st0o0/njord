## ADDED Requirements

### Requirement: Enrichment and Mqtt follow the library reference rules
`Njord.Enrichment` and `Njord.Mqtt` SHALL depend only on `Njord.Core`, `Njord.Messages`, `Njord.Persistence` and `Njord.Domain` (never on each other, never on any other feature library, never on the `Njord` host). `Njord.Enrichment` SHALL NOT contain Home Assistant or MQTT knowledge.

#### Scenario: Enrichment does not know the egress protocol
- **WHEN** a type in `Njord.Enrichment` gains a dependency on `Njord.Mqtt` or `Njord.Grpc`
- **THEN** the architecture test run fails and names the offending type and dependency

#### Scenario: Mqtt does not know enrichment features
- **WHEN** a type in `Njord.Mqtt` gains a dependency on a type in `Njord.Enrichment` (for example `IEnrichmentFeature`)
- **THEN** the architecture test run fails and names the offending type and dependency

#### Scenario: Lateral reference between feature libraries
- **WHEN** a type in `Njord.Enrichment` or `Njord.Mqtt` gains a dependency on a type in another feature library
- **THEN** the architecture test run fails and names the offending type, the dependency and both assemblies
