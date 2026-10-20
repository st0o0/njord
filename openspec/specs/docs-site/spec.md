# docs-site Specification

## Purpose

A VitePress static documentation site at njord.st0o0.net providing getting-started guides, configuration reference, model catalog, MQTT topic reference, and Home Assistant integration guide.
## Requirements
### Requirement: Site is built with VitePress and served as static files
The documentation site SHALL be a VitePress project at `docs/` (pnpm, `docs/package.json`).
It SHALL build to static HTML/CSS/JS with no server-side runtime. It SHALL be
deployable to GitHub Pages at `njord.st0o0.net`. The VitePress config SHALL use
`withLikeC4()` from `@leberkas-org/vitepress-likec4` to wrap the config, and
SHALL set `themeConfig.logo` to `/logo.svg`.

#### Scenario: VitePress builds successfully
- **WHEN** `pnpm build` is executed in `docs/`
- **THEN** a `docs/.vitepress/dist/` directory is produced with static HTML files

#### Scenario: Site is accessible at custom domain
- **WHEN** the site is deployed to GitHub Pages
- **THEN** it is accessible at `njord.st0o0.net`

#### Scenario: LikeC4 diagrams render in built site
- **WHEN** the built site is served
- **THEN** pages with `<likec4-view>` tags show interactive architecture diagrams

### Requirement: Getting Started page covers Docker setup
The site SHALL include a `/getting-started/` page explaining how to run njord via Docker with a minimal configuration, including the required MQTT broker host and at least one location and model.

#### Scenario: Minimal config example
- **WHEN** a user reads the Getting Started page
- **THEN** they find a copy-pasteable minimal `appsettings.json` and `docker run` command

### Requirement: Model catalog lists all known models
The site SHALL include a `/models/` page listing all models from `ModelCoverageRegistry` with: model ID, coverage tier, region description, geographic bounds, maximum forecast hours, and temporal resolution notes.

#### Scenario: Model detail shows coverage and horizon
- **WHEN** a user looks up `icon_d2` in the model catalog
- **THEN** they see: Regional, DE/CH/AT, bounds 43-57°N 1-18°E, max 60h, 1h resolution

### Requirement: MQTT reference documents topic scheme and payloads
The site SHALL include an `/mqtt-reference/` page documenting the topic structure (`{baseTopic}/{location}/{model}/{horizon}` and `{baseTopic}/{location}/{feature}[/{sub}]`), the state payload JSON format, and the availability topic. It SHALL NOT document MQTT Discovery payloads, because njord publishes none.

#### Scenario: Topic scheme documented
- **WHEN** a user reads the MQTT reference
- **THEN** they understand the topic hierarchy and can predict topic names for their config

### Requirement: Home Assistant guide covers integration
The site SHALL include a `/home-assistant/` page covering: how njord entities appear in HA, recommended `recorder:` exclude patterns for `sensor.njord_*`, and example dashboard cards.

#### Scenario: Recorder exclude documented
- **WHEN** a user reads the HA guide
- **THEN** they find a copy-pasteable `recorder:` YAML snippet

### Requirement: Architecture page in sidebar navigation
The VitePress sidebar SHALL include an "Architecture" link under the "Guide"
section, pointing to `/architecture`.

#### Scenario: Architecture link in sidebar
- **WHEN** a user views any docs page
- **THEN** the sidebar shows "Architecture" as a navigable link under "Guide"

### Requirement: Hero section displays logo
The docs landing page hero section SHALL display the njord logo above the
title text.

#### Scenario: Hero shows logo
- **WHEN** a user visits the docs landing page
- **THEN** the hero section shows the njord logo image above "njord"

### Requirement: Custom CSS applies brand colors
A `custom.css` file SHALL be imported by the VitePress theme to override default
brand colors with the njord blue palette.

#### Scenario: Theme uses blue brand colors
- **WHEN** the docs site is loaded
- **THEN** all VitePress brand-colored elements (links, buttons, accents) use
  blue tones instead of default purple

### Requirement: Releases deploy the documentation site
The `docs` job of `.github/workflows/release.yml` SHALL build and deploy the VitePress site to GitHub Pages through the shared `st0o0/github-workflows` `docs.yml` workflow whenever release-please creates a release.

#### Scenario: Release triggers deployment
- **WHEN** a push to main makes release-please create a release
- **THEN** the `docs` job builds and deploys the site to GitHub Pages

