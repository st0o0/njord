## MODIFIED Requirements

### Requirement: EnergyResult serialization with pinned wire names
`EnergyResult` SHALL have `[property: JsonProperty("...")]` on all positional parameters. The value tuple `(int HoursFromNow, double Cop)` in the `CopOptimal` list SHALL be replaced with a named record (`CopOptimalEntry`) carrying `[JsonProperty]` attributes.

#### Scenario: EnergyResult with CopOptimal entries round-trips through JSON
- **WHEN** an `EnergyResult` with CopOptimal entries, CopEstimate, and CopOptimalConservative is serialized and deserialized
- **THEN** all properties round-trip correctly, CopOptimal entries use named record fields with camelCase wire names
