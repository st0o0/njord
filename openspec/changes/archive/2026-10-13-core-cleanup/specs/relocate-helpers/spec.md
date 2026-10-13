# relocate-helpers Delta

## MODIFIED Requirements

### Requirement: ModelCoverageRegistry lives in Njord.Weather
`ModelCoverageRegistry`, `ModelCoverage`, `BoundingBox`, and `CoverageTier`
SHALL reside in `Njord.Weather`, not `Njord.Core`. They describe weather-model
metadata with no Core dependencies.

#### Scenario: Coverage lookup works from any consumer
- **GIVEN** a project that references `Njord.Weather`
- **WHEN** it calls `ModelCoverageRegistry.Get("icon_d2")`
- **THEN** it receives `ModelCoverage` with the correct tier, bounds, and max hours

#### Scenario: Plausibility check remains functional
- **GIVEN** a location at lat=47.05, lon=8.31 (Lucerne)
- **WHEN** `ModelCoverageRegistry.IsPlausible("meteoswiss_icon_ch2", 47.05, 8.31)` is called
- **THEN** it returns `true`

### Requirement: HorizonProjection lives in Njord.Weather
`HorizonProjection` SHALL reside in `Njord.Weather`, not `Njord.Core/Egress`.
It operates exclusively on Weather types.

#### Scenario: Horizon payload built from Forecast
- **GIVEN** a `Forecast` with hourly data spanning 72 hours
- **WHEN** `HorizonProjection.Build` is called with horizons `[3, 6, 12]`
- **THEN** it returns JSON payloads keyed `"h3"`, `"h6"`, `"h12"` with the correct parameter values

### Requirement: TopicSlug lives in Njord.Mqtt
`TopicSlug` SHALL reside in `Njord.Mqtt`, not `Njord.Core/Egress`. Its only
production consumer is `TopicScheme`.

#### Scenario: Slug generation for location names
- **GIVEN** a location name `"Zürich HB"`
- **WHEN** `TopicSlug.Slugify` is called
- **THEN** it returns `"z_rich_hb"`
