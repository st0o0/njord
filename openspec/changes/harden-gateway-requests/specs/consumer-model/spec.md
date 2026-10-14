## ADDED Requirements

### Requirement: Read-model queries match locations case-insensitively
`ForecastReadModel` SHALL match the location of `QueryForecast` and `QueryLatestAnalysis` against the stored locations case-insensitively, as it already does for model ids, so a query spelled differently from the configured location returns the same data as one spelled exactly.

#### Scenario: Forecast query in another spelling
- **WHEN** a forecast for (lucerne, icon_d2) has been received and `QueryForecast(LUCERNE, "icon_d2")` arrives
- **THEN** the read model replies `ForecastResult` with that forecast

#### Scenario: Analysis query in another spelling
- **WHEN** an alert event for `lucerne` has been received and `QueryLatestAnalysis(LUCERNE)` arrives
- **THEN** the read model replies `LatestAnalysisResult` carrying that alert event
