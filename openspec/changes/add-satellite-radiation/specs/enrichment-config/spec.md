## ADDED Requirements

### Requirement: SatelliteRadiation toggle
`EnrichmentOptions` SHALL expose a `SatelliteRadiation` toggle that defaults to false. The Config Builder, `docs/data/enrichment.json` and `docker-compose.example.yml` SHALL mirror it, and its API-call cost (about 720 calls/month/location) SHALL be documented.

#### Scenario: Default off
- **WHEN** no configuration is supplied
- **THEN** `SatelliteRadiation` SHALL be false and no satellite request SHALL be made

#### Scenario: Builder parity
- **WHEN** the toggle list in `EnrichmentOptions` is compared with the Config Builder data
- **THEN** both SHALL contain `SatelliteRadiation`
