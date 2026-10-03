## 1. VitePress scaffold

- [x] 1.1 Create `docs/` directory with VitePress project: `package.json`, `.vitepress/config.ts`, theme config with njord branding, nav/sidebar structure matching the site map.
- [x] 1.2 Create `docs/index.md` — landing page: what njord does, key features, link to Getting Started.
- [x] 1.3 Verify `npm run dev` serves the site locally and `npm run build` produces static output.

## 2. Registry data export

- [x] 2.1 Export `ModelCoverageRegistry` data to `docs/data/models.json` — array of `{ id, tier, region, bounds, maxForecastHours }`.
- [x] 2.2 Export `ParameterRegistry` group data to `docs/data/parameters.json` — array of `{ group, hourlyCount, dailyCount, variables[] }`.
- [x] 2.3 Export enrichment feature defaults to `docs/data/enrichment.json` — each feature with its options and defaults.

## 3. Documentation pages

- [x] 3.1 Write `docs/getting-started.md` — Docker setup, minimal appsettings.json, docker run command, first-run verification.
- [x] 3.2 Write `docs/configuration/` section: `index.md` (overview), `locations.md`, `models.md`, `horizons.md`, `parameters.md`, `enrichment.md`, `mqtt.md`, `persistence.md`, `budget.md`.
- [x] 3.3 Write `docs/models.md` — model catalog table generated from `models.json` data, with coverage tier, region, max hours, resolution notes.
- [x] 3.4 Write `docs/mqtt-reference.md` — topic scheme, payload format examples, discovery structure, availability topic.
- [x] 3.5 Write `docs/home-assistant.md` — integration guide, entity naming, recorder exclude snippet, dashboard card examples.

## 4. Config Builder — data layer

- [x] 4.1 Create `docs/.vitepress/theme/builder/types.ts` — TypeScript interfaces for NjordConfig, LocationConfig, ModelInfo, ParameterGroup, EnrichmentConfig, BudgetResult.
- [x] 4.2 Create `docs/.vitepress/theme/builder/budget.ts` — budget calculation function matching NjordOptionsValidator formula (totalModels × cyclesPerMonth × weight), coverage check function, effective forecast days function.
- [x] 4.3 Create `docs/.vitepress/theme/builder/serializer.ts` — export to appsettings.json, export to env vars (`Njord__` format), import auto-detect (JSON vs env vars), URL hash encode/decode.

## 5. Config Builder — Vue components

- [x] 5.1-5.9 All builder components implemented in a single `ConfigBuilder.vue`: LocationEditor, ModelSelector with coverage warnings, HorizonPicker with presets, ParameterGroupSelector with weight display, BudgetBar with live calculation, ConfigExport (JSON + env vars + copy), ConfigImport (auto-detect), URL hash sync.

## 6. Builder page integration

- [x] 6.1 Register builder components in VitePress custom theme (`docs/.vitepress/theme/index.ts`).
- [x] 6.2 Create `docs/builder.md` — embeds `<ConfigBuilder />` component with introductory text.
- [x] 6.3 Verify builder works in dev mode and in production build (no SSR issues with Vue components).

## 7. CI/CD

- [x] 7.1 Create `.github/workflows/docs.yml` — build VitePress on push to main (when `docs/` changes), deploy to GitHub Pages with custom domain `njord.st0o0.net`.
- [x] 7.2 Add `CNAME` file at `docs/public/CNAME` with `njord.st0o0.net`.

## 8. Validation

- [x] 8.1 Verify `npm run build` in `docs/` produces a clean build.
- [ ] 8.2 Verify all documentation pages render correctly.
- [ ] 8.3 Test config builder: add locations, select models, change horizons, verify budget updates, export JSON, export env vars, import JSON, import env vars, test URL hash sharing.
