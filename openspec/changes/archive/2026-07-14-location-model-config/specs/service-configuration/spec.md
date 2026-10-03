## MODIFIED Requirements

### Requirement: LocationOptions supports per-location model list
The `LocationOptions` class SHALL have an optional `Models` property
(`IList<string>?`) that lists model IDs specific to this location. When
set, these models SHALL be merged with the global `Models` list to produce
the effective model set for this location.

#### Scenario: Location with Models in JSON config
- **WHEN** appsettings.json contains `{ "Name": "berlin", "Models": ["icon_d2"] }`
- **THEN** `LocationOptions.Models` SHALL contain `["icon_d2"]`

#### Scenario: Location without Models in JSON config
- **WHEN** appsettings.json contains `{ "Name": "amsterdam" }` with no
  Models property
- **THEN** `LocationOptions.Models` SHALL be null

### Requirement: Budget calculation accounts for per-location model counts
The startup budget validation SHALL compute projected API usage as the
sum of resolved model counts per location (not global
`locations.Count × models.Count`). Each location may have a different
number of effective models.

#### Scenario: Two locations with different model counts
- **WHEN** global Models has 3 entries, location A adds 1 model, and
  location B adds 2 models
- **THEN** projected requests per cycle SHALL be (3+1) + (3+2) = 9,
  not 2 × 3 = 6
