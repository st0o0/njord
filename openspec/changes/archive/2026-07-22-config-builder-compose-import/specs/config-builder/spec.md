## ADDED Requirements

### Requirement: Import docker-compose environment blocks
The builder SHALL accept pasted docker-compose YAML containing an `environment:` block with `- KEY=VALUE` list items. Lines with leading whitespace and `- ` prefix SHALL be stripped before parsing. Non-`Njord__` lines SHALL be ignored. The full compose service block (including `image:`, `volumes:`, etc.) SHALL be accepted — only environment lines are extracted.

#### Scenario: Import compose environment block
- **WHEN** a user pastes a docker-compose snippet containing `- Njord__PollInterval=01:00:00` and `- Njord__Models__0=icon_d2`
- **THEN** the builder populates PollInterval as "01:00:00" and shows icon_d2 in the model list

#### Scenario: Import full compose service block
- **WHEN** a user pastes a complete compose service block including `image:`, `volumes:`, and `environment:` sections
- **THEN** only the environment variables are parsed; non-environment YAML is ignored

#### Scenario: Mixed Njord and non-Njord env vars
- **WHEN** a user pastes a compose environment block containing `- TZ=Europe/Berlin` and `- Njord__ForecastDays=4`
- **THEN** only `Njord__ForecastDays=4` is imported; `TZ` is ignored

## MODIFIED Requirements

### Requirement: Import existing environment variables
The builder SHALL accept pasted environment variables (one `KEY=VALUE` per line, keys starting with `Njord__`) and populate the builder fields. Non-Njord keys SHALL be ignored. The builder SHALL also accept docker-compose `environment:` syntax where lines are prefixed with `- ` and optional whitespace.

#### Scenario: Import env vars
- **WHEN** a user pastes lines containing `Njord__Mqtt__Host=192.168.1.1` and `Njord__Models__0=icon_d2`
- **THEN** the builder shows MQTT host as 192.168.1.1 and icon_d2 in the model list

#### Scenario: Import compose-style env vars
- **WHEN** a user pastes lines like `      - Njord__Horizons__0=3` with leading whitespace and list markers
- **THEN** the builder strips the formatting and imports the horizons correctly
