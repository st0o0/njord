## MODIFIED Requirements

### Requirement: Layer reference direction
Foundation libraries SHALL follow this reference direction: `Domain` and `Persistence` reference no Njord assembly; `Messages` references only `Domain`; `Compute` references only `Domain`; `Core` references only `Domain`, `Messages`, `Persistence`, and `Compute`. Feature libraries reference only `Core` and below. The host references all.

#### Scenario: Compute references only Domain
- **WHEN** `LayerReferenceSpec` checks `Njord.Compute` assembly references
- **THEN** the only Njord reference SHALL be `Njord.Domain`

#### Scenario: Core references include Compute
- **WHEN** `LayerReferenceSpec` checks `Njord.Core` assembly references
- **THEN** the allowed Njord references SHALL be `Njord.Domain`, `Njord.Messages`, `Njord.Persistence`, and `Njord.Compute`

#### Scenario: Feature libraries reference Core and below
- **WHEN** `LayerReferenceSpec` checks a feature library's assembly references
- **THEN** the allowed set SHALL include `Njord.Compute` (transitively via Core and below)

### Requirement: Base library registration
The architecture test infrastructure SHALL list `Njord.Compute` as a base library alongside `Njord.Core`, `Njord.Domain`, `Njord.Messages`, and `Njord.Persistence`.

#### Scenario: BaseLibraryNames includes Compute
- **WHEN** `NjordArchitecture.BaseLibraryNames` is inspected
- **THEN** it SHALL contain `"Njord.Compute"`
