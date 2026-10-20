## ADDED Requirements

### Requirement: Verify is reserved for wire-format contracts
Verify snapshot testing SHALL only be used in Persistence.Tests and Grpc.Tests for pinning serialized wire formats. All other test projects SHALL use explicit assertions.

#### Scenario: New test needs shape verification outside Persistence/Grpc
- **WHEN** a test in any project other than Persistence.Tests or Grpc.Tests needs to verify a serialized shape (JSON payload, options structure)
- **THEN** the test SHALL use explicit Assert-based verification, not Verify snapshots

#### Scenario: Unused Verify dependencies
- **WHEN** a test project references Verify.XunitV3 but has zero Verify calls
- **THEN** the package reference and ModuleInitializer SHALL be removed

### Requirement: MQTT payload shape tests use explicit assertions
MQTT payload shape tests SHALL verify structure through explicit JSON assertions rather than Verify snapshots.

#### Scenario: MQTT payload golden master replacement
- **WHEN** replacing Verify-based MQTT payload tests
- **THEN** the replacement tests SHALL assert the same JSON structure (member names, null vs omitted, value types) using JsonNode/JsonDocument assertions

### Requirement: Options defaults tested by validators not snapshots
Options default values SHALL be validated by the existing validator tests, not pinned by Verify snapshots.

#### Scenario: DefaultOptionsSpec without Verify
- **WHEN** the Verify golden master is removed from DefaultOptionsSpec
- **THEN** the validator-passes test and override-wins test SHALL remain as the default-correctness checks
