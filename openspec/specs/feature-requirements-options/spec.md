# feature-requirements-options Specification

## Purpose
This capability has been replaced. The resolved feature requirements are now `DomainRequirements` registered directly as `IOptions<DomainRequirements>` (see domain-requirements); ensemble and forecast model lists live in `Njord:Ensemble` and `Njord:Forecast` (see ensemble-api-options, forecast-api-options). `FeatureRequirementsOptions` and `FeatureResolution` no longer exist.

## Requirements

### Requirement: Capability superseded

The system SHALL NOT contain any type named `FeatureRequirementsOptions` or `FeatureResolution`. This capability has been superseded by `domain-requirements`, `ensemble-api-options` and `forecast-api-options`.

#### Scenario: No runtime effect
- **WHEN** the system starts
- **THEN** no `FeatureRequirementsOptions` or `FeatureResolution` type SHALL exist
