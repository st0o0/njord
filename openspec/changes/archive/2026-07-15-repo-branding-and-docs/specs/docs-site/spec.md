## MODIFIED Requirements

### Requirement: Site is built with VitePress and served as static files
The documentation site SHALL be a VitePress project at `docs/` in the repo root.
It SHALL build to static HTML/CSS/JS with no server-side runtime. It SHALL be
deployable to GitHub Pages at `njord.st0o0.net`. The VitePress config SHALL use
`withLikeC4()` from `@leberkas-org/vitepress-likec4` to wrap the config, and
SHALL set `themeConfig.logo` to `/logo.svg`.

#### Scenario: VitePress builds successfully
- **WHEN** `npm run docs:build` is executed in the repo root (or `docs/`)
- **THEN** a `docs/.vitepress/dist/` directory is produced with static HTML files

#### Scenario: Site is accessible at custom domain
- **WHEN** the site is deployed to GitHub Pages
- **THEN** it is accessible at `njord.st0o0.net`

#### Scenario: LikeC4 diagrams render in built site
- **WHEN** the built site is served
- **THEN** pages with `<likec4-view>` tags show interactive architecture diagrams

## ADDED Requirements

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
