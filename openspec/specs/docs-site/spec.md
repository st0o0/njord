# docs-site Specification

## Purpose

A VitePress static documentation site at njord.st0o0.net providing getting-started guides, configuration reference, model catalog, MQTT topic reference, and Home Assistant integration guide.

## Requirements

### Requirement: Site is built with VitePress and served as static files
The documentation site SHALL be a VitePress project at `docs/` in the repo root. It SHALL build to static HTML/CSS/JS with no server-side runtime. It SHALL be deployable to GitHub Pages at `njord.st0o0.net`.

#### Scenario: VitePress builds successfully
- **WHEN** `npm run build` is executed in `docs/`
- **THEN** a `docs/.vitepress/dist/` directory is produced with static HTML files

#### Scenario: Site is accessible at custom domain
- **WHEN** the site is deployed to GitHub Pages
- **THEN** it is accessible at `njord.st0o0.net`

### Requirement: Getting Started page covers Docker setup
The site SHALL include a `/getting-started/` page explaining how to run njord via Docker with a minimal configuration, including the required MQTT broker host and at least one location and model.

#### Scenario: Minimal config example
- **WHEN** a user reads the Getting Started page
- **THEN** they find a copy-pasteable minimal `appsettings.json` and `docker run` command

### Requirement: Configuration reference documents all options
The site SHALL include a `/configuration/` section with sub-pages for each configuration area: locations, models, horizons, parameters, enrichment (with each feature), MQTT, persistence, and budget. Each option SHALL list its type, default value, and validation constraints.

#### Scenario: Enrichment feature documentation
- **WHEN** a user navigates to the enrichment configuration page
- **THEN** each feature (consensus, alerts, derived, trends, indices, energy, history) is documented with its options and defaults

### Requirement: Model catalog lists all known models
The site SHALL include a `/models/` page listing all models from `ModelCoverageRegistry` with: model ID, coverage tier, region description, geographic bounds, maximum forecast hours, and temporal resolution notes.

#### Scenario: Model detail shows coverage and horizon
- **WHEN** a user looks up `icon_d2` in the model catalog
- **THEN** they see: Regional, DE/CH/AT, bounds 43-57°N 1-18°E, max 48h, 1h resolution

### Requirement: MQTT reference documents topic scheme and payloads
The site SHALL include an `/mqtt-reference/` page documenting the topic structure (`{baseTopic}/{location}/{model}/{horizon}`), payload JSON format, discovery config structure, and availability topic.

#### Scenario: Topic scheme documented
- **WHEN** a user reads the MQTT reference
- **THEN** they understand the topic hierarchy and can predict topic names for their config

### Requirement: Home Assistant guide covers integration
The site SHALL include a `/home-assistant/` page covering: how njord entities appear in HA, recommended `recorder:` exclude patterns for `sensor.njord_*`, and example dashboard cards.

#### Scenario: Recorder exclude documented
- **WHEN** a user reads the HA guide
- **THEN** they find a copy-pasteable `recorder:` YAML snippet

### Requirement: GitHub Actions deploys the site
A GitHub Actions workflow SHALL build the VitePress site on push to main and deploy to GitHub Pages. The workflow SHALL be at `.github/workflows/docs.yml`.

#### Scenario: Push triggers deployment
- **WHEN** a commit is pushed to main that changes files in `docs/`
- **THEN** the GitHub Actions workflow builds and deploys the updated site
