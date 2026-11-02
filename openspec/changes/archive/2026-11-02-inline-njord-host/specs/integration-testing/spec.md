## MODIFIED Requirements

### Requirement: The real host is started in integration tests
`Njord.IntegrationTests` SHALL start the production composition (the same setups `Program.cs` applies) on a test server with test configuration: a SQLite journal in a unique temporary directory, a fake Open-Meteo client, and fixed locations and models. The test host SHALL inline the setup chain from `Program.cs` and override domain clients with fakes between the configure and build steps. It SHALL assert the health endpoints and the metrics endpoint of the running host.

#### Scenario: Host becomes ready
- **WHEN** the host starts with valid test configuration and one poll cycle has completed through the fake client
- **THEN** `/healthz` and `/alive` return success and the metrics endpoint exposes the `njord_` meters

#### Scenario: Invalid configuration
- **WHEN** the host starts with an invalid configuration (for example a missing location)
- **THEN** startup fails with the validation message and the test asserts that message
