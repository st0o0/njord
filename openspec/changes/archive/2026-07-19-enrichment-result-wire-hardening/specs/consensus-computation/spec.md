## MODIFIED Requirements

### Requirement: ConsensusResult serialization with pinned wire names
`ConsensusResult`, `ParameterConsensus`, and `HorizonConsensus` records SHALL have `[property: JsonProperty("...")]` on all positional parameters. Value tuple properties on `HorizonConsensus` (`Outlier`, `ConfidenceInterval`) SHALL be replaced with named records (`OutlierInfo`, `ConfidenceIntervalInfo`) carrying `[JsonProperty]` attributes. `WeatherModel` and `ParameterDef` domain types referenced in these records SHALL also have `[property: JsonProperty]` on their serialized properties.

#### Scenario: ConsensusResult with hourly and daily parameters round-trips through JSON
- **WHEN** a `ConsensusResult` with parameter consensus entries containing horizon consensus data (including outlier and confidence interval) is serialized and deserialized
- **THEN** all properties round-trip correctly with camelCase wire names, including the replaced tuple fields and nested domain type properties
