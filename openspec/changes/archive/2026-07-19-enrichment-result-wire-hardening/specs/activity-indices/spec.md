## MODIFIED Requirements

### Requirement: IndexResult serialization with pinned wire names
`IndexResult` and `ScoreEnvelope` records SHALL have `[property: JsonProperty("...")]` on all positional parameters. Value tuple properties `FrostProtection` and `Vpd` SHALL be replaced with named records (`FrostProtectionInfo`, `VpdInfo`) carrying `[JsonProperty]` attributes.

#### Scenario: IndexResult with all fields round-trips through JSON
- **WHEN** an `IndexResult` with FrostProtection, Vpd, and ScoreEnvelope values is serialized and deserialized
- **THEN** all properties round-trip correctly, including the replaced tuple fields using named record types with camelCase wire names

#### Scenario: IndexResult with null optional fields round-trips
- **WHEN** an `IndexResult` with null FrostProtection, Vpd, and null envelope fields is serialized and deserialized
- **THEN** all null values are preserved and non-null fields round-trip correctly
