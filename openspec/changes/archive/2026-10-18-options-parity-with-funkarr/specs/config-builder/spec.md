## MODIFIED Requirements

### Requirement: Import docker-compose environment blocks
The builder SHALL accept pasted docker-compose YAML containing an `environment:` block with `- KEY=VALUE` list items. Lines with leading whitespace and `- ` prefix SHALL be stripped before parsing. Non-`Njord__` lines SHALL be ignored. The full compose service block (including `image:`, `volumes:`, etc.) SHALL be accepted — only environment lines are extracted. The builder SHALL read and write the per-concern keys (`Njord__Polling__PollInterval`, `Njord__OpenMeteo__BaseUrl`, `Njord__Storage__PersistencePath`); the removed root-level keys SHALL be reported as outdated on import.

#### Scenario: Import compose environment block
- **WHEN** a user pastes a docker-compose snippet containing `- Njord__Polling__PollInterval=01:00:00` and `- Njord__Models__0=icon_d2`
- **THEN** the builder populates PollInterval as "01:00:00" and shows icon_d2 in the model list

#### Scenario: Import full compose service block
- **WHEN** a user pastes a complete compose service block including `image:`, `volumes:`, and `environment:` sections
- **THEN** only the environment variables are parsed; non-environment YAML is ignored

#### Scenario: Mixed Njord and non-Njord env vars
- **WHEN** a user pastes a compose environment block containing `- TZ=Europe/Berlin` and `- Njord__ForecastDays=4`
- **THEN** only `Njord__ForecastDays=4` is imported; `TZ` is ignored

#### Scenario: Outdated root-level key
- **WHEN** a user pastes `- Njord__PollInterval=01:00:00`
- **THEN** the builder reports the key as outdated and names `Njord__Polling__PollInterval`
