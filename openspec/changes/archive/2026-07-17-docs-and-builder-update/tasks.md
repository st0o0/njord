## 1. Tagline and Description Updates

- [x] 1.1 Update `docs/.vitepress/config.ts` — change `description` from "Open-Meteo weather API → MQTT bridge for Home Assistant" to "Multi-model weather intelligence for Home Assistant"
- [x] 1.2 Update `docs/index.md` — change hero tagline, review feature cards (consensus is now implemented, not aspirational)
- [x] 1.3 Update `README.md` — update subtitle and body text to reflect enrichment pipeline, daily forecasts, persistence
- [x] 1.4 Update `CLAUDE.md` — update project description paragraph, remove "Consensus is deferred" decision, mark landed decisions as implemented

## 2. Config Builder: Enrichment Section

- [x] 2.1 Add Enrichment section to `docs/.vitepress/theme/builder/ConfigBuilder.vue` — import enrichment.json, render toggles per feature with enabled-by-default state, show/hide option inputs when toggled on
- [x] 2.2 Wire enrichment toggle state into `config.enrichment` reactive object — enabled features get `{ Enabled: true, ...options }`, disabled features are omitted
- [x] 2.3 Add CSS for enrichment cards consistent with existing model-card and param-card styles

## 3. Config Builder: Persistence and MQTT Auth

- [x] 3.1 Add Persistence section to ConfigBuilder.vue — SQLite/PostgreSQL radio, connection string input when PostgreSQL selected
- [x] 3.2 Add MQTT Username and Password fields to the existing MQTT section in General — password field uses `type="password"`
- [x] 3.3 Add Discovery Interval field to the General section

## 4. Config Builder: docker-compose.yml Export

- [x] 4.1 Add `exportAsCompose(config)` function to `docs/.vitepress/theme/builder/serializer.ts` — generates valid docker-compose.yml with image, restart, volume, and env vars from `exportAsEnvVars()`
- [x] 4.2 Add "docker-compose.yml" as third tab in export section of ConfigBuilder.vue — wire to `exportAsCompose()`, copy button works with all three formats

## 5. Validation

- [x] 5.1 Run `npm run dev` in `docs/` and verify: builder loads, enrichment toggles work, all three export formats produce valid output, URL hash sharing preserves enrichment state
- [x] 5.2 Run `npm run build` in `docs/` to verify VitePress build succeeds with all changes
