## MODIFIED Requirements

### Requirement: ParameterRegistry provides static typed properties
The `ParameterRegistry` SHALL expose a static, non-nullable `ParameterDef` member for each parameter, declared once as a typed `static readonly` field whose initializer registers the entry in the catalog. A parameter SHALL NOT be looked up by a string at type initialization.

#### Scenario: Typed property returns correct ParameterDef
- **WHEN** `ParameterRegistry.Temperature2m` is accessed
- **THEN** it SHALL return a `ParameterDef` with `ApiName == "temperature_2m"`

#### Scenario: All domain-referenced parameters have typed properties
- **WHEN** all static members are accessed
- **THEN** each SHALL return a non-null `ParameterDef` that is part of the catalog

#### Scenario: Missing parameter fails at startup
- **WHEN** two declarations share the same API name and granularity
- **THEN** the catalog specification fails, and a duplicate cannot reach production because the uniqueness spec runs in CI
