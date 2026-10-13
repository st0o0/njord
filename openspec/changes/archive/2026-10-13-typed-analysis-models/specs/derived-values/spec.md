## MODIFIED Requirements

### Requirement: Dew-point comfort category
The dew-point comfort computation SHALL accept a `double?` dew-point temperature in °C and return a nullable typed comfort category. Categories SHALL be: Dry (< 10 °C), Comfortable (10–15 °C), Sticky (16–18 °C), Oppressive (19–21 °C), Dangerous (> 21 °C). If the input is `null`, the result SHALL be `null`. The published text of each category SHALL be its lowercase name ("dry", "comfortable", "sticky", "oppressive", "dangerous"), unchanged from before.

#### Scenario: Dry air
- **WHEN** dew point is 5.0 °C
- **THEN** the comfort category is Dry

#### Scenario: Comfortable
- **WHEN** dew point is 12.0 °C
- **THEN** the comfort category is Comfortable

#### Scenario: Sticky
- **WHEN** dew point is 17.0 °C
- **THEN** the comfort category is Sticky

#### Scenario: Oppressive
- **WHEN** dew point is 20.0 °C
- **THEN** the comfort category is Oppressive

#### Scenario: Dangerous
- **WHEN** dew point is 23.0 °C
- **THEN** the comfort category is Dangerous

#### Scenario: Boundary at 10
- **WHEN** dew point is 10.0 °C
- **THEN** the comfort category is Comfortable

#### Scenario: Null input
- **WHEN** dew point is null
- **THEN** the result is null

#### Scenario: Published text unchanged
- **WHEN** a Sticky comfort category is published via MQTT
- **THEN** the payload value is `sticky`

### Requirement: WMO weather description from weather code
The WMO computation SHALL accept an `int?` WMO 4677 weather code and return a nullable typed weather condition. The mapping SHALL cover codes 0–99. Unknown codes SHALL return `null`. A `null` input SHALL return `null`. The English description of each condition SHALL be produced at the output edge and SHALL equal the description published before this change.

#### Scenario: Clear sky
- **WHEN** weather code is 0
- **THEN** the condition's description is "Clear sky"

#### Scenario: Mainly clear
- **WHEN** weather code is 1
- **THEN** the condition's description is "Mainly clear"

#### Scenario: Rain slight
- **WHEN** weather code is 61
- **THEN** the condition's description is "Rain: slight"

#### Scenario: Thunderstorm with heavy hail
- **WHEN** weather code is 99
- **THEN** the condition's description is "Thunderstorm with heavy hail"

#### Scenario: Unknown code
- **WHEN** weather code is 150
- **THEN** the result is null

#### Scenario: Null input
- **WHEN** weather code is null
- **THEN** the result is null

## REMOVED Requirements

### Requirement: DerivedResult serialization with pinned wire names
**Reason**: Derived result records are in-process values only; nothing serializes or persists them.
**Migration**: The published wire format is pinned by `wire-format-golden-masters`.
