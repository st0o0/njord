## ADDED Requirements

### Requirement: Integration removal cleans up entities

The E2E test plan SHALL verify that removing the njord integration from HA cleans up all entities.

#### Scenario: Remove integration via HA UI
- **WHEN** the njord integration is removed via Settings → Devices & Services → njord → Delete
- **THEN** all njord entities (weather, sensor, binary_sensor, event, button) are removed from HA within 30 seconds

#### Scenario: No orphaned entities after removal
- **WHEN** the njord integration has been removed
- **THEN** `GET /api/states` returns no entities with "njord" in their device identifiers

### Requirement: Stack teardown verification

The E2E test plan SHALL verify clean shutdown of the Docker stack.

#### Scenario: Docker compose down removes containers
- **WHEN** `docker compose -f e2e/docker-compose.e2e.yml down -v` is run
- **THEN** both njord and homeassistant containers are removed, and named volumes are deleted

#### Scenario: Re-integration after teardown
- **WHEN** the stack is brought up again after a full teardown (`down -v` + `up -d --build`)
- **THEN** HA requires fresh onboarding (no persisted state from previous run) and the njord integration can be added from scratch
