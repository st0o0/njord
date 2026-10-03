## MODIFIED Requirements

### Requirement: DerivedResult serialization with pinned wire names
`DerivedResult`, `HorizonDerived`, and `ScalarDerived` records SHALL have `[property: JsonProperty("...")]` on all positional parameters producing camelCase wire names.

#### Scenario: DerivedResult round-trips through JSON
- **WHEN** a `DerivedResult` with horizon-derived values and scalar-derived values is serialized and deserialized
- **THEN** all properties (including nested HorizonDerived and ScalarDerived fields) round-trip correctly with camelCase wire names
