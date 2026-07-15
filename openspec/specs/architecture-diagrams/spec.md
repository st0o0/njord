# architecture-diagrams Specification

## Purpose

LikeC4 architecture diagrams embedded in the documentation site, providing interactive C4-model views of njord's system context, internal zones, and streaming pipeline.

## Requirements

### Requirement: LikeC4 model defines njord architecture
A LikeC4 C4 model SHALL exist at `docs/likec4/` containing at minimum:
- `specification.c4` defining element kinds and visual styles
- `model.c4` defining architectural elements (Open-Meteo API, njord service
  with Ingest/Domain/Egress zones, MQTT broker, Home Assistant)
- `views.c4` defining diagram views

#### Scenario: Model compiles without errors
- **WHEN** `npx likec4 validate` is run from `docs/`
- **THEN** the model compiles successfully with no errors

### Requirement: System context view shows end-to-end data flow
The LikeC4 model SHALL include an `index` view showing the high-level data
flow: Open-Meteo API -> njord -> MQTT Broker -> Home Assistant.

#### Scenario: Index view renders four elements
- **WHEN** the `index` view is rendered
- **THEN** it shows Open-Meteo API, njord, MQTT Broker, and Home Assistant
  as distinct elements with directional relationships

### Requirement: Internals view shows three-zone architecture
The LikeC4 model SHALL include an `internals` view showing njord's internal
zones: Ingest, Domain, and Egress, with their key components.

#### Scenario: Internals view shows zone separation
- **WHEN** the `internals` view is rendered
- **THEN** Ingest, Domain, and Egress appear as separate containers within njord

### Requirement: Pipeline view shows Akka.Streams flow
The LikeC4 model SHALL include a `pipeline` view showing the Akka.Streams
processing pipeline stages from tick source through MQTT publish.

#### Scenario: Pipeline view shows sequential stages
- **WHEN** the `pipeline` view is rendered
- **THEN** the stages appear in order: tick -> fan-out -> throttle -> HTTP ->
  aggregate -> enrich -> MQTT

### Requirement: VitePress integrates LikeC4 via plugin
The VitePress config SHALL use `@leberkas-org/vitepress-likec4` to integrate
LikeC4 diagrams. Diagrams SHALL be embeddable in markdown pages via
`<likec4-view view-id="..." />` tags.

#### Scenario: Interactive diagram in docs page
- **WHEN** a docs page containing `<likec4-view view-id="index" />` is loaded
- **THEN** an interactive architecture diagram renders inline

### Requirement: Architecture page exists in docs
The documentation site SHALL include an `/architecture` page embedding the
LikeC4 views with explanatory text about njord's design.

#### Scenario: Architecture page is navigable
- **WHEN** a user clicks "Architecture" in the docs sidebar
- **THEN** they see a page with embedded LikeC4 diagrams and architecture
  explanations

### Requirement: Static SVG export for README
A static SVG export of the `index` view SHALL be available at
`docs/public/architecture.svg` for embedding in the GitHub README (which
cannot render custom web components).

#### Scenario: SVG export matches current model
- **WHEN** `npx likec4 export --format svg` is run
- **THEN** `docs/public/architecture.svg` is produced matching the current
  `index` view
