# Capability: aspire-test-fixture

## Purpose

Shared Aspire-based test fixture: boots the full Njord host with Mosquitto, WireMock, and gRPC via `DistributedApplicationTestingBuilder`, providing a single long-lived application instance for integration and E2E test collections.

## Requirements

### Requirement: Shared Aspire test fixture
The test infrastructure SHALL provide a shared fixture class that creates a `DistributedApplication` from the `Njord.AppHost` using `DistributedApplicationTestingBuilder`. The fixture SHALL implement `IAsyncLifetime` and start the application once per test collection. It SHALL expose the WireMock Admin API URL, MQTT connection options (host/port), and gRPC channel to the Njord service.

#### Scenario: Fixture starts all infrastructure
- **WHEN** the shared fixture initializes
- **THEN** Mosquitto, WireMock, and Njord containers/projects SHALL be running
- **AND** the Njord service SHALL be healthy (responding to health checks)

#### Scenario: Fixture exposes WireMock admin endpoint
- **WHEN** a test accesses the fixture's WireMock admin API
- **THEN** it SHALL be able to post mappings and query request logs via `IWireMockAdminApi`

#### Scenario: Fixture exposes MQTT connection details
- **WHEN** a test accesses the fixture's MQTT options
- **THEN** `Host` and `Port` SHALL point to the Aspire-managed Mosquitto container

#### Scenario: Fixture exposes gRPC channel
- **WHEN** a test accesses the fixture's gRPC channel
- **THEN** it SHALL be able to call `ConfigService` RPCs (including `TriggerPoll`) on the running Njord instance

### Requirement: Test collection shares one fixture instance
All integration and E2E tests within a test project SHALL share a single fixture instance via xUnit `ICollectionFixture<T>`. The `DistributedApplication` SHALL be started once and disposed after all tests complete.

#### Scenario: Multiple test classes share infrastructure
- **WHEN** two test classes in the same collection run
- **THEN** both SHALL use the same Mosquitto, WireMock, and Njord instances

### Requirement: WireMock mapping reset between tests
Tests SHALL reset WireMock mappings before configuring their own fixtures to avoid cross-test interference. The fixture SHALL provide convenient access to the `IWireMockAdminApi` for this purpose.

#### Scenario: Test resets prior mappings
- **WHEN** a test calls `ResetMappingsAsync` on the WireMock admin API before configuring its fixtures
- **THEN** only the current test's mappings SHALL be active
