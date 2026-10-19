# enrichment-framework Specification

## Purpose

The enrichment framework for the multi-domain proxy architecture: IEnrichment<TInput> for typed enrichment evaluation, IProjector<TInput> for projecting LocationForecast into typed inputs, and ProjectionGroup<TInput> for bundling a projector with its enrichments. Enables reactive evaluation where only groups affected by a domain update are evaluated.

## Requirements

### Requirement: Enrichments declare typed inputs via Projectors
IEnrichment<TInput> SHALL have an Evaluate method taking TInput and TimeProvider. IProjector<TInput> SHALL have a Project method taking (LocationForecast current, LocationForecast? previous) returning TInput? (null when required domains are missing). IProjector SHALL declare RequiredDomains.

#### Scenario: Projector returns null when domain missing
- **WHEN** a Projector requires Weather and AirQuality but LocationForecast has only Weather
- **THEN** Project SHALL return null and the enrichment SHALL NOT be evaluated

### Requirement: ProjectionGroups bundle Projector with Enrichments
ProjectionGroup<TInput> SHALL hold one IProjector<TInput> and a list of IEnrichment<TInput>. Evaluation SHALL project once and evaluate all enrichments in the group.

#### Scenario: One projection serves multiple enrichments
- **WHEN** a WeatherAlertGroup has FrostAlert and StormAlert enrichments
- **THEN** the WeatherAlertProjector projects once and both enrichments evaluate the same input

### Requirement: Reactive evaluation based on changed domain
The pipeline SHALL only evaluate ProjectionGroups whose RequiredDomains include the domain that was just updated. Groups not affected by the update SHALL be skipped.

#### Scenario: AirQuality update skips Weather-only group
- **WHEN** AirQuality data arrives
- **THEN** a ProjectionGroup that requires only Weather SHALL NOT be evaluated
