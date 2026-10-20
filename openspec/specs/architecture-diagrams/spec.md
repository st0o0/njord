# architecture-diagrams Specification

## Purpose

LikeC4 architecture diagrams embedded in the documentation site, providing interactive C4-model views of njord's system context, internal zones, and streaming pipeline.
## Requirements
### Requirement: System context view shows end-to-end data flow
The LikeC4 model SHALL include an `index` view showing the high-level data flow: Open-Meteo API -> njord -> Home Assistant (gRPC via ha-njord), and njord -> MQTT broker as the optional second output.

#### Scenario: Index view renders four elements
- **WHEN** the `index` view is rendered
- **THEN** it shows Open-Meteo API, njord, Home Assistant and the MQTT broker as distinct elements with directional relationships, the Home Assistant relationship labeled gRPC

### Requirement: Internals view shows three-zone architecture
The LikeC4 model SHALL include an `internals` view showing njord's three zones — Ingest, Domain and Consumers — with their key components.

#### Scenario: Internals view shows zone separation
- **WHEN** the `internals` view is rendered
- **THEN** Ingest (gateway and client), Domain (scheduler, pipeline, analysis) and Consumers (MQTT state publisher, read model and gRPC API) appear as separate containers within njord

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

### Requirement: Static diagram image for README
A static PNG export of the `index` view SHALL be available at `docs/public/index.png` for embedding in the GitHub README (which cannot render custom web components). It SHALL be regenerated with `pnpm export:diagrams` (`likec4 export png -o public/ --flat`) in `docs/` whenever the `index` view changes.

#### Scenario: PNG export matches current model
- **WHEN** `pnpm export:diagrams` is run in `docs/`
- **THEN** `docs/public/index.png` is produced matching the current `index` view

