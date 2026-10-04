## Purpose

Provides HA-standard "Download Diagnostics" support so users and developers can export a debug snapshot of the integration's state.

## Requirements

### Requirement: Config entry diagnostics are downloadable
The integration SHALL provide `async_get_config_entry_diagnostics` in a `diagnostics.py` module. The returned dict SHALL contain sections for config entry data, coordinator state, stream status, and server status.

#### Scenario: User downloads diagnostics
- **WHEN** the user clicks "Download Diagnostics" on the integration page
- **THEN** a JSON file is downloaded containing all diagnostic sections

### Requirement: Diagnostics include redacted config
The config entry data section SHALL include `host` redacted (replaced with `**REDACTED**`) and `port` in cleartext.

#### Scenario: Host is redacted
- **WHEN** diagnostics are downloaded for a config entry with host "192.168.1.100"
- **THEN** the config section shows `host: "**REDACTED**"` and `port: 50051`

### Requirement: Diagnostics include coordinator state summary
The coordinator state section SHALL include: number of tracked locations, number of tracked forecast keys (location×model pairs), enrichment types present per location, and coordinator last update timestamp.

#### Scenario: Coordinator state with data
- **WHEN** diagnostics are downloaded and the coordinator has 2 locations with 3 models each
- **THEN** the coordinator section shows `locations: 2`, `forecast_keys: 6`, and enrichment types per location

### Requirement: Diagnostics include stream connection states
The stream status section SHALL include the connection state (connected/disconnected) for each stream type (forecast, enrichment, config).

#### Scenario: All streams connected
- **WHEN** diagnostics are downloaded and all streams are connected
- **THEN** the stream section shows `forecast: connected`, `enrichment: connected`, `config: connected`

### Requirement: Diagnostics include server status
The server status section SHALL include version, uptime, and budget usage if the status coordinator has data. If unavailable, the section SHALL indicate `"unavailable"`.

#### Scenario: Server status available
- **WHEN** diagnostics are downloaded and status coordinator has data
- **THEN** the server section shows version, uptime_seconds, and budget usage percent

#### Scenario: Server status unavailable
- **WHEN** diagnostics are downloaded but the status coordinator has no data
- **THEN** the server section shows `"unavailable"`
