## MODIFIED Requirements

### Requirement: WireMock container fixture for Open-Meteo simulation
The test infrastructure SHALL provide access to a WireMock instance managed by the Aspire AppHost fixture. Tests SHALL configure WireMock responses via the `IWireMockAdminApi` client using the fixture's exposed admin URL. The fixture SHALL serve the existing JSON fixture files (`openmeteo-icon_eu-96h.json`, `openmeteo-icon_d2-96h.json`) as canned responses when configured by individual tests.

#### Scenario: WireMock is available via shared fixture
- **WHEN** a test class using the Aspire fixture accesses the WireMock admin API
- **THEN** WireMock SHALL be running and accepting mapping configurations

#### Scenario: WireMock responds with fixture JSON for matching model requests
- **WHEN** a test configures WireMock to respond to `/v1/forecast` with `models=icon_eu`
- **THEN** WireMock SHALL respond with HTTP 200 and the contents of `openmeteo-icon_eu-96h.json`

#### Scenario: WireMock can simulate error responses
- **WHEN** a test configures WireMock to return HTTP 429 for a specific path
- **THEN** subsequent requests to that path SHALL receive HTTP 429

### Requirement: OpenMeteoClient integration tests against WireMock
The test project SHALL include integration tests that exercise `OpenMeteoClient` against the Aspire-managed WireMock instance, validating request construction and response parsing over a real network connection. Tests SHALL use the Njord host's OpenMeteoClient implicitly (for E2E) or create their own client pointing to the fixture's WireMock URL (for component integration tests).

#### Scenario: Successful fetch through real HTTP connection
- **WHEN** `OpenMeteoClient.FetchAsync` is called with a location and model configured in WireMock
- **THEN** the result SHALL be a `FetchOutcome.Success` with correctly parsed `ModelForecast` data matching the fixture

#### Scenario: Request URL construction is correct
- **WHEN** `OpenMeteoClient.FetchAsync` sends a request to WireMock
- **THEN** the WireMock request log SHALL show a GET to `/v1/forecast` with query parameters `latitude`, `longitude`, `models`, `hourly`, `wind_speed_unit=ms`, `timeformat=unixtime`, `forecast_days=4`

#### Scenario: Rate limiting through real HTTP
- **WHEN** WireMock is configured to return HTTP 429 and `FetchAsync` is called
- **THEN** the result SHALL be `FetchOutcome.Failure` with reason `RateLimited`

#### Scenario: Model unavailable through real HTTP
- **WHEN** WireMock is configured to return HTTP 400 with an error payload and `FetchAsync` is called
- **THEN** the result SHALL be `FetchOutcome.Failure` with reason `ModelUnavailable`

### Requirement: Full E2E pipeline integration test
The test project SHALL include an end-to-end test that boots the full Njord host via the Aspire fixture and exercises the complete data path: WireMock serves forecast JSON → Njord fetches via pipeline → domain maps → egress projects → MQTT publishes to Mosquitto. The test SHALL trigger the poll cycle via gRPC `TriggerPoll` and assert on MQTT retained messages.

#### Scenario: Single poll cycle produces correct retained MQTT messages
- **WHEN** WireMock fixtures are loaded, `TriggerPoll` is called via gRPC, and the test waits for MQTT retained messages
- **THEN** the Mosquitto broker SHALL have retained messages on the correct horizon state topics with valid JSON payloads containing forecast data

#### Scenario: Discovery device configs are published
- **WHEN** the E2E test triggers a poll cycle and the Njord host processes it
- **THEN** the Mosquitto broker SHALL have retained device config messages under `homeassistant/device/njord_<location>_<model>/config` with the correct component count

#### Scenario: Availability topic reflects online status
- **WHEN** the Njord host is running and connected to Mosquitto
- **THEN** the `njord/status` topic SHALL contain "online"

### Requirement: Docker tests run by default
All container-based integration tests SHALL run without requiring environment variable gates. The `NJORD_DOCKER_TESTS` gate SHALL be removed from `MqttEgressIntegrationSpec`.

#### Scenario: MqttEgressIntegrationSpec runs without gate
- **WHEN** the test suite is executed without setting `NJORD_DOCKER_TESTS`
- **THEN** `MqttEgressIntegrationSpec` SHALL execute (not skip)
