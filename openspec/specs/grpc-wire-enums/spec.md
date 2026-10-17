# grpc-wire-enums Specification

## Purpose
TBD - created by archiving change typed-wire-enums. Update Purpose after archive.
## Requirements
### Requirement: Closed sets are proto enums
The gRPC contract SHALL express these closed sets as proto enums, each with a `*_UNSPECIFIED = 0` member: poll phase (`discovery`, `steady`), trend direction (`rising`, `falling`, `stable`), stability (`converging`, `diverging`, `stable`), dew point comfort (`dry`, `comfortable`, `sticky`, `oppressive`, `dangerous`), VPD category (`low`, `optimal`, `high`, `critical`) and consensus method (`mean`, `median`, `trimmed_mean`). The former `string` fields' numbers SHALL be `reserved` and the enum fields SHALL keep the field names with new field numbers.

#### Scenario: Poll phase on the wire
- **WHEN** a `ModelStatus` or `TriggerTarget` is built for a worker in the steady phase
- **THEN** its `phase` is the `PollPhase` steady member, not text

#### Scenario: Optional value not delivered
- **WHEN** a horizon has no dew point comfort
- **THEN** the optional `dewpoint_comfort` field is absent

#### Scenario: Old string fields are reserved
- **WHEN** the protos are compiled
- **THEN** the removed string fields' numbers are `reserved` and not reused

### Requirement: Domain to wire mapping is exhaustive
Every mapping from a domain enum to a proto enum or wire text SHALL be an exhaustive switch that throws for an undefined value. No wire or metric string SHALL be produced with `ToString()`, `ToLowerInvariant()` or `Enum.TryParse` on a domain enum.

#### Scenario: New domain member without mapping
- **WHEN** a member is added to a mapped domain enum without extending its mapping
- **THEN** a mapping test fails instead of producing a default value

#### Scenario: MQTT severity text unchanged
- **WHEN** an alert with severity orange is published over MQTT
- **THEN** the payload contains `"orange"`, produced by `AlertSeverity.ToWireName()`

### Requirement: Consensus method over gRPC is validated
`SetEnrichment` SHALL accept `ConsensusConfig.method` as the `ConsensusMethod` enum. `UNSPECIFIED` or absent SHALL leave the method unchanged; an undefined numeric value SHALL produce a Rejected `ConfigResponse` and change nothing. `GetConfig` and `StreamConfig` SHALL report the configured method as the enum.

#### Scenario: Set a valid method
- **WHEN** a client sends `method = TRIMMED_MEAN`
- **THEN** the configured consensus method becomes trimmed mean

#### Scenario: Unknown method value
- **WHEN** a client sends an undefined numeric method value
- **THEN** the response is Rejected and the configuration is unchanged

### Requirement: Clients tolerate unknown enum values
The contract SHALL state that clients treat unknown enum numbers as `UNSPECIFIED`, so adding members is not a breaking change.

#### Scenario: New member added later
- **WHEN** njord adds a new member to one of these enums
- **THEN** existing clients keep working and treat it as unspecified

