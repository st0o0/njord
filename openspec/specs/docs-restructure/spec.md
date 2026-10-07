# docs-restructure Specification

## Purpose

Presents njord as a complete system (service + Home Assistant integration) rather than service-only documentation, with dedicated Home Assistant, Development, and Reference sections.

## Requirements

### Requirement: Landing page presents full njord concept

The `docs/index.md` hero and features section SHALL present njord as a complete system: a .NET weather service plus a native Home Assistant integration. It SHALL mention that the integration lives in a separate repo (`ha-njord`).

#### Scenario: Landing page describes both components

- **WHEN** a visitor opens the docs landing page
- **THEN** the hero text and feature cards reference both the service and the HA integration as parts of one system

### Requirement: Dedicated Home Assistant documentation section

The sidebar SHALL include a "Home Assistant" group with pages for installation, entity reference, options, and diagnostics.

#### Scenario: HA section in sidebar

- **WHEN** a visitor navigates the sidebar
- **THEN** a "Home Assistant" group is visible with at least: Installation, Entities, Options, Diagnostics

### Requirement: Entity reference page

A `docs/home-assistant/entities.md` page SHALL document all entity platforms (weather, sensor, binary_sensor, event, button), the enrichment-based sensors (alerts, indices, trends, derived, history), and the server diagnostic sensors.

#### Scenario: Entity page lists all platforms

- **WHEN** a visitor opens the entities page
- **THEN** each platform (weather, sensor, binary_sensor, event, button) is documented with entity naming, attributes, and what data it exposes

### Requirement: Development documentation section

The sidebar SHALL include a "Development" group with pages covering dev setup, .NET service development, HA integration development, and proto management.

#### Scenario: Dev section in sidebar

- **WHEN** a contributor navigates the sidebar
- **THEN** a "Development" group is visible with at least: Dev Setup, Service, Integration, Protos

### Requirement: Dev setup page with docker-compose

A `docs/development/setup.md` page SHALL include a `docker-compose.yml` example that runs both njord and Home Assistant together, with `custom_components/njord` mounted read-only into the HA container.

#### Scenario: Dev setup page has working compose

- **WHEN** a contributor reads the dev setup page
- **THEN** they find a docker-compose example, clone instructions for both repos, and how to run the full stack locally

### Requirement: Reference section groups lookup material

The sidebar SHALL include a "Reference" group containing the model catalog, MQTT topics, and config builder pages.

#### Scenario: Reference section consolidates lookup pages

- **WHEN** a visitor navigates the sidebar
- **THEN** Model Catalog, MQTT Topics, and Config Builder are grouped under "Reference"

### Requirement: Architecture page covers full system

The architecture page SHALL describe data flow from Open-Meteo through the service to consumers (gRPC → HA integration, MQTT → other consumers). It SHALL reference ha-njord as the HA integration repo.

#### Scenario: Architecture page shows end-to-end flow

- **WHEN** a visitor reads the architecture page
- **THEN** the diagram and text show the complete path: API → service → gRPC → HA integration → HA entities
