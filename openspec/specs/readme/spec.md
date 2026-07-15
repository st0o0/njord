# readme Specification

## Purpose

A polished GitHub README presenting njord's identity, capabilities, quick-start instructions, and architecture at a glance.

## Requirements

### Requirement: README has centered logo and title
The README SHALL display the njord logo centered above a centered `<h1>` title
and a one-line tagline.

#### Scenario: Logo visible on GitHub
- **WHEN** the repository is viewed on GitHub
- **THEN** the logo, title "njord", and tagline are displayed centered at the
  top of the README

### Requirement: README has badge row
The README SHALL include a row of badges below the tagline showing: license
type, .NET version, and a link to the documentation site.

#### Scenario: Badges render on GitHub
- **WHEN** the repository is viewed on GitHub
- **THEN** badges are visible as inline images with correct links

### Requirement: README has feature highlights
The README SHALL include a concise feature section (4-6 bullet points)
summarizing njord's key capabilities: multi-model forecasts, multiple
locations, enrichment features, MQTT auto-discovery, and low resource usage.

#### Scenario: Features section is scannable
- **WHEN** a user reads the README
- **THEN** they can understand njord's value proposition within 10 seconds
  from the feature bullets

### Requirement: README has minimal quick start
The README SHALL include a quick-start section with a docker-compose example
(under 20 lines) that gets njord running with one location and two models.

#### Scenario: Quick start is copy-pasteable
- **WHEN** a user copies the docker-compose snippet
- **THEN** they have a working configuration after replacing the MQTT host

### Requirement: README has architecture diagram
The README SHALL embed the static SVG architecture diagram showing the
Open-Meteo -> njord -> MQTT -> HA data flow.

#### Scenario: Architecture diagram visible on GitHub
- **WHEN** the repository is viewed on GitHub
- **THEN** the architecture diagram renders as an inline image

### Requirement: README links to full documentation
The README SHALL include a link to the VitePress documentation site with a
brief description of what the docs cover.

#### Scenario: Documentation link is prominent
- **WHEN** a user reads the README
- **THEN** they find a clearly visible link to the full documentation

### Requirement: README includes license and attribution
The README SHALL state the project license and include attribution to
Open-Meteo (CC BY 4.0 data license).

#### Scenario: Open-Meteo attribution present
- **WHEN** a user reads the README footer
- **THEN** they see the Open-Meteo data license attribution
