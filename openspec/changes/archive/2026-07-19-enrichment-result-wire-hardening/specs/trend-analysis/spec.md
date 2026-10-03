## MODIFIED Requirements

### Requirement: TrendResult serialization with pinned wire names
`TrendResult`, `ParameterTrend`, and `WeatherChangeResult` records SHALL have `[property: JsonProperty("...")]` on all positional parameters. Value tuple properties (`PrecipTiming`, `ExtremaTiming`, `Stability`, `Decay`) SHALL be replaced with named records (`PrecipTimingInfo`, `ExtremaTimingInfo`, `StabilityInfo`, `DecayInfo`) carrying `[JsonProperty]` attributes.

#### Scenario: TrendResult with all fields round-trips through JSON
- **WHEN** a `TrendResult` with parameter trends, weather change, and all timing/stability/decay fields is serialized and deserialized
- **THEN** all properties round-trip correctly with camelCase wire names, including the replaced tuple fields using named record types
