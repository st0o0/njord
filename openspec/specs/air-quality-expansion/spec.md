### Requirement: Air quality exposes additional variables
`AirQualityParams` and `AirQualityPoint` SHALL cover `carbon_monoxide`, `sulphur_dioxide`, `dust`, `ammonia`, `olive_pollen`, `us_aqi`, `uv_index` and `uv_index_clear_sky`, each as a flag and a nullable point member. Every API-name mapping, response mapping and `HasFields` switch SHALL have one arm per member and throw `ArgumentOutOfRangeException` for unknown values.

#### Scenario: Request contains requested variables
- **WHEN** the requested params include `Dust | CarbonMonoxide`
- **THEN** the air-quality request SHALL contain `dust` and `carbon_monoxide` in `hourly` and SHALL still be a single call

#### Scenario: Response values are mapped
- **WHEN** the response contains `dust` values for an hour
- **THEN** the mapped `AirQualityPoint` for that hour SHALL carry the `Dust` value and variables absent from the response SHALL be null

#### Scenario: Missing field detection
- **WHEN** a point has no `Dust` value and `HasFields(Dust)` is called
- **THEN** it SHALL return false

### Requirement: Region selects region-bound variables
`AirQualityOptions.Region` (`Global`, `Europe`, `UnitedStates`, default `Global`) SHALL decide which region-bound variables are requested. `Europe` SHALL add `ammonia` and `olive_pollen`; `UnitedStates` SHALL add `us_aqi`; `Global` SHALL add none of them. An unknown value SHALL fail options validation at startup.

#### Scenario: Europe region
- **WHEN** `Region` is `Europe` and AirQuality is fetched
- **THEN** the request SHALL contain `ammonia` and `olive_pollen` and SHALL NOT contain `us_aqi`

#### Scenario: Default region
- **WHEN** `Region` is not configured
- **THEN** the request SHALL contain none of `ammonia`, `olive_pollen` and `us_aqi`

### Requirement: Dust alert enrichment
The system SHALL provide a `DustAlert` enrichment (toggle default false) that raises an alert from forecast `dust` concentration using three-step severity thresholds from `Njord:Enrichment:Alerts`. It SHALL require the `Dust` param and trigger the AirQuality domain.

#### Scenario: Dust above the first threshold
- **WHEN** `DustAlert` is enabled and forecast dust within the alert window exceeds the first threshold
- **THEN** an alert of type Dust with the matching severity SHALL be raised

#### Scenario: Toggle off
- **WHEN** `DustAlert` is disabled
- **THEN** no Dust alert SHALL be raised and no handler SHALL require `dust`

### Requirement: No API-budget change
Adding variables SHALL NOT change the number of air-quality calls per location per poll cycle.

#### Scenario: Budget projection unchanged
- **WHEN** all new flags are requested
- **THEN** the `BudgetCalculator` projection SHALL equal the projection without them
