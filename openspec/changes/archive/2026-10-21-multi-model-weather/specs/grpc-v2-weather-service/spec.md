## ADDED Requirements

### Requirement: Per-model weather data
The weather service SHALL accept an optional `model` in `GetDomainDataRequest` and SHALL set `model` on every weather `DomainDataUpdate`. An empty request model SHALL select the primary model. `StreamDomainData` SHALL emit one weather update per effective model.

#### Scenario: Default request
- **WHEN** a client calls `GetDomainData` for Weather without a model
- **THEN** the response carries the primary model's data and its model id

#### Scenario: Named model
- **WHEN** a client requests an enabled secondary model
- **THEN** the response carries that model's points and `model` set to its id

#### Scenario: Unknown or disabled model
- **WHEN** a client requests a model that is not effective
- **THEN** the call fails with `NOT_FOUND`

#### Scenario: Non-weather domains
- **WHEN** air quality, flood or marine data is served
- **THEN** the `model` field is empty

### Requirement: Catalog reports enabled models
`GetCatalog` SHALL list the catalog models with an `enabled` flag that is true exactly for the effective models.

#### Scenario: Two enabled models
- **WHEN** two of five catalog models are effective
- **THEN** exactly those two have `enabled = true`
