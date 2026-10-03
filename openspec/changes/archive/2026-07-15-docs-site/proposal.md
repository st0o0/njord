## Why

njord's configuration has grown to 60+ settings across 8 sections (locations, models, horizons, parameters, enrichment, MQTT, persistence, budget). There is no documentation beyond code comments and the example appsettings. Users need to understand model coverage, forecast horizons, API budget impact, and enrichment features before they can write a working config. An interactive configuration builder — embedded in a documentation site — would let users assemble their config visually with live budget feedback, then copy the result as `appsettings.json` or environment variables.

## What Changes

- **VitePress documentation site** at `njord.st0o0.net`, built from `docs/` in the repo root. Static hosting via GitHub Pages, built via GitHub Actions.
- **Documentation pages**: Getting Started (Docker, minimal config), Configuration Reference (per-section deep dive), Model Catalog (all known models with coverage, resolution, max horizon), MQTT Reference (topic scheme, payloads), Home Assistant Guide (integration, recorder excludes).
- **Interactive Config Builder** on `/builder/` — Vue components embedded in VitePress:
  - Location picker (name + lat/lon)
  - Model selector with coverage warnings and max-horizon display
  - Horizon presets (Standard, Fine, Custom)
  - Parameter group selector with API weight impact
  - Enrichment feature toggles with settings
  - Live budget calculator (same formula as `NjordOptionsValidator`)
  - Export as `appsettings.json` or environment variables (copy-to-clipboard)
  - Import existing `appsettings.json` or env vars to populate the builder
- **Registry data export**: `ModelCoverageRegistry` and `ParameterRegistry` data exported as static JSON files consumed by the builder components.

### API budget impact

Zero — the docs site is a static site with no runtime dependency on njord or Open-Meteo.

## Capabilities

### New Capabilities

- `docs-site`: VitePress documentation site with static content pages, model catalog, and configuration reference.
- `config-builder`: Interactive Vue-based configuration builder with import/export, live budget validation, and model coverage awareness.

### Modified Capabilities

(none)

## Non-goals

- Server-side logic or live connection to a running njord instance.
- Internationalization (English only).
- Automated sync between codebase and docs — registry data is exported manually when models change.
- Authentication or user accounts.

## Impact

- **New directory**: `docs/` at repo root containing the VitePress project.
- **New CI**: GitHub Actions workflow for building and deploying to GitHub Pages.
- **No changes to njord service code** — the builder uses exported static data, not runtime APIs.
- **Dependencies**: VitePress, Vue 3 (bundled with VitePress).
