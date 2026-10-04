## MODIFIED Requirements

### Requirement: Config flow validates gRPC connection
The config flow SHALL validate the gRPC connection by calling `get_catalog()` during setup. Tests SHALL exercise the real HA config flow machinery using `hass.config_entries.flow`.

#### Scenario: Successful connection creates entry
- **WHEN** user submits host and port that connect successfully
- **THEN** `get_catalog()` is called, and a config entry is created with `FlowResultType.CREATE_ENTRY`

#### Scenario: Connection failure shows error
- **WHEN** user submits host and port that fail to connect (gRPC error)
- **THEN** the form is shown again with `errors == {"base": "cannot_connect"}`

#### Scenario: Duplicate host:port aborts
- **WHEN** user submits a host:port combination that is already configured
- **THEN** the flow aborts with `FlowResultType.ABORT` and reason `already_configured`
