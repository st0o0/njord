## ADDED Requirements

### Requirement: Nine-zone solution folder structure
The `.slnx` file SHALL organize projects into nine solution folders reflecting the architecture zones:

| Folder | Projects |
|--------|----------|
| `/Model/` | `Njord.Model`, `Njord.Sensors` |
| `/Forecast/` | `Njord.Forecast` |
| `/Contracts/` | `Njord.Consensus`, `Njord.Messages` |
| `/Infrastructure/` | `Njord.Persistence`, `Njord.Core` |
| `/Ingest/` | `Njord.OpenMeteo`, `Njord.SensorHub`, `Njord.Budget` |
| `/Analysis/` | `Njord.Alerts`, `Njord.Derived`, `Njord.Trends`, `Njord.Indices` |
| `/Processing/` | `Njord.Pipeline` |
| `/Egress/` | `Njord.Mqtt`, `Njord.Grpc` |
| `/Tests/` | All `Njord.*Tests` projects and `Njord.Tests.Shared` |

The host project (`Njord/`) SHALL remain at the solution root, outside any folder.

#### Scenario: IDE displays zone grouping
- **WHEN** opening `Njord.slnx` in an IDE
- **THEN** projects SHALL appear grouped under their zone folder

#### Scenario: No project appears in multiple folders
- **WHEN** inspecting the `.slnx` file
- **THEN** each project SHALL appear in exactly one `<Folder>` element

### Requirement: ArchUnit zone enforcement updated
`ZoneArchitectureSpec` SHALL enforce the new zone assignments, verifying that:
- Analysis features reference only Core (never Forecast directly)
- Egress projects reference only Core
- Ingest projects reference only Core
- Processing (Pipeline) references Core plus analysis libraries

#### Scenario: Analysis references Forecast directly
- **WHEN** `Njord.Alerts` adds a `<ProjectReference>` to `Njord.Forecast`
- **THEN** `ZoneArchitectureSpec` SHALL fail

#### Scenario: Egress references Pipeline directly
- **WHEN** `Njord.Mqtt` adds a `<ProjectReference>` to `Njord.Pipeline`
- **THEN** `ZoneArchitectureSpec` SHALL fail

### Requirement: LayerReferenceSpec updated
`LayerReferenceSpec` SHALL enforce the updated reference direction:
- `Njord.Model` and `Njord.Sensors`: no Njord project references
- `Njord.Forecast`: only `Njord.Model`
- `Njord.Consensus`: only `Njord.Model` + `Njord.Forecast`
- `Njord.Messages`: only `Njord.Model` + `Njord.Sensors` + `Njord.Consensus` + `Njord.Forecast`
- `Njord.Core`: only `Njord.Model` + `Njord.Forecast` + `Njord.Messages` + `Njord.Persistence` + `Njord.Consensus`
- Feature libraries: only `Njord.Core` (plus Pipeline's analysis library references)

#### Scenario: Correct reference rules compile and pass
- **WHEN** running `LayerReferenceSpec`
- **THEN** all reference direction rules SHALL pass with the new project layout

### Requirement: CLAUDE.md solution structure updated
The `CLAUDE.md` file SHALL reflect the new project layout (Model, Forecast replacing Weather), the new reference direction, the new `.slnx` folder zones, and updated test counts.

#### Scenario: Documentation matches reality
- **WHEN** `DocumentationArchitectureSpec` runs
- **THEN** it SHALL pass, confirming that the documented structure, file names, spec names, and test counts match the actual solution
