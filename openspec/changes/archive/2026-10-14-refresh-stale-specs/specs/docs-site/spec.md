## ADDED Requirements

### Requirement: Releases deploy the documentation site
The `docs` job of `.github/workflows/release.yml` SHALL build and deploy the VitePress site to GitHub Pages through the shared `st0o0/github-workflows` `docs.yml` workflow whenever release-please creates a release.

#### Scenario: Release triggers deployment
- **WHEN** a push to main makes release-please create a release
- **THEN** the `docs` job builds and deploys the site to GitHub Pages

## MODIFIED Requirements

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

### Requirement: Configuration reference documents all options
The site SHALL include a `/configuration/` section with sub-pages for each configuration area: locations, models, horizons, parameters, analysis features (enrichment), MQTT, persistence, and budget. Each option SHALL list its type, default value, and validation constraints, and SHALL match an existing options property.

#### Scenario: Enrichment feature documentation
- **WHEN** a user navigates to the enrichment configuration page
- **THEN** each feature (consensus, alerts, derived, trends, indices) is documented with its options and defaults, and no removed feature (such as history) is listed

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

## REMOVED Requirements

### Requirement: GitHub Actions deploys the site
**Reason**: The requirement named types, files or behavior that no longer exist; it is restated against the current code.
**Migration**: Superseded by "Releases deploy the documentation site" in this capability.
