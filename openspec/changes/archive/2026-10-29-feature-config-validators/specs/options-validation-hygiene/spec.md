# options-validation-hygiene Specification (Delta)

## ADDED Requirements

### Requirement: Domain options validators live beside their option classes

`ForecastOptionsValidator`, `EnsembleOptionsValidator`, and `FloodOptionsValidator` SHALL reside in `Njord.Core/Configuration/` beside the option classes they validate, not in `Njord.OpenMeteo`.

#### Scenario: Validators co-located with options
- **WHEN** `ForecastOptions` is defined in `Njord.Core/Configuration/`
- **THEN** `ForecastOptionsValidator` SHALL also reside in `Njord.Core/Configuration/`

#### Scenario: Registration moves to Core
- **WHEN** validators are registered for ForecastOptions, EnsembleOptions, and FloodOptions
- **THEN** registration SHALL happen in `CoreSetupContainer` (or an extension method in Core), not in `OpenMeteoSetupContainer`

#### Scenario: Validation behavior unchanged
- **WHEN** any existing valid or invalid configuration is validated after the move
- **THEN** the validation result SHALL be identical to before the move
