## ADDED Requirements

### Requirement: Tests run against real HA runtime
All tests SHALL use `pytest-homeassistant-custom-component` to provide a real HomeAssistant instance. No hand-rolled HA module stubs SHALL exist in the test infrastructure.

#### Scenario: Import from homeassistant works natively
- **WHEN** a test file imports from `homeassistant.core` or any `homeassistant.*` module
- **THEN** the real HA module is loaded (not a stub)

#### Scenario: conftest.py contains no HA module stubs
- **WHEN** `tests/conftest.py` is examined
- **THEN** it contains NjordClient mock fixtures and no `sys.modules` manipulation for homeassistant modules

### Requirement: NjordClient mock fixture
Tests SHALL use a `mock_client` fixture that patches `NjordClient` at the import path used by the integration. The fixture SHALL return an `AsyncMock` with canned responses for all client methods.

#### Scenario: Mock client provides default responses
- **WHEN** a test requests the `mock_client` fixture
- **THEN** `get_config`, `get_locations`, `get_models`, `get_forecast`, `get_enrichments`, `connect`, and `close` are all available as `AsyncMock` with sensible default return values

#### Scenario: Mock client is patchable for error paths
- **WHEN** a test sets `mock_client.get_locations.side_effect = Exception("fail")`
- **THEN** the integration code sees the exception when calling the client

### Requirement: Tests run in Docker
Tests SHALL be runnable via a Docker command without local Python. The command SHALL install `pytest-homeassistant-custom-component` and all dependencies.

#### Scenario: make test runs all tests
- **WHEN** `make test` is executed
- **THEN** all tests run in a Docker container and report results

### Requirement: Integration setup/teardown tests
Tests SHALL verify that `async_setup_entry` and `async_unload_entry` work correctly with the real HA machinery.

#### Scenario: Setup creates coordinator and platforms
- **WHEN** `async_setup_entry` is called with a valid config entry
- **THEN** the coordinator is created, platforms are forwarded, and entities appear in `hass.states`

#### Scenario: Unload closes gRPC client
- **WHEN** `async_unload_entry` is called
- **THEN** the gRPC client's `close()` method is called and platforms are unloaded
