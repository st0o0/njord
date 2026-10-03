## Why

njord has evolved from a simple Open-Meteo-to-MQTT bridge into a weather intelligence service with enrichment pipeline, persistence, daily forecasts, and gRPC. The documentation and Config Builder still reflect the early "bridge" identity — the tagline "Open-Meteo weather API → MQTT bridge for Home Assistant" appears in README, docs landing page, VitePress config, and CLAUDE.md. The Config Builder lacks UI for enrichment (7 features with options), persistence provider selection, and docker-compose.yml export. CLAUDE.md contains stale decisions (e.g. "Consensus is deferred") that no longer reflect reality.

## What Changes

- **Tagline update** — replace "Open-Meteo weather API → MQTT bridge for Home Assistant" with a description that reflects enrichment, multi-model intelligence, and the full feature set across: README.md, docs/index.md (hero), docs/.vitepress/config.ts (site description)
- **CLAUDE.md cleanup** — remove/update stale Decisions entries (consensus deferral, enrichment-not-yet status), update project description
- **Config Builder: Enrichment section** — add toggles for all 7 features (Consensus, Alerts, Derived, Trends, Indices, Energy, History) with their configurable options, driven by docs/data/enrichment.json
- **Config Builder: Persistence section** — SQLite (default) vs PostgreSQL selection with connection string input
- **Config Builder: MQTT auth** — expose Username and Password fields (types.ts already has them, UI does not)
- **Config Builder: Discovery Interval** — expose in General section
- **Config Builder: docker-compose.yml export** — third export format alongside JSON and env vars, generates a working compose file with volume mount and environment variables

## Non-goals

- No changes to the .NET service code
- No changes to docs content pages (Getting Started, Configuration/*, Enrichment, MQTT Reference, Models, Home Assistant, Architecture) — these are already accurate
- No changes to data files (models.json, enrichment.json, parameters.json)
- No API budget impact — this change is documentation and tooling only

## Capabilities

### New Capabilities
- `config-builder-enrichment`: UI section in the Config Builder for toggling and configuring all 7 enrichment features with their options
- `config-builder-compose-export`: docker-compose.yml export format in the Config Builder, generating a working compose file from the current configuration

### Modified Capabilities
<!-- No spec-level behavior changes — this is docs/tooling only -->

## Impact

- **Files modified**: README.md, CLAUDE.md, docs/index.md, docs/.vitepress/config.ts, docs/.vitepress/theme/builder/ConfigBuilder.vue, docs/.vitepress/theme/builder/serializer.ts, docs/.vitepress/theme/builder/types.ts
- **No runtime impact** — purely documentation and developer tooling
- **Dependencies**: none added
