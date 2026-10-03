## 1. Logo & Brand Assets

- [x] 1.1 Create `docs/public/logo.svg` — geometric wind-rose/compass SVG (48×48 viewBox, monochrome, 8 directional rays, works at 16/32/120px)
- [x] 1.2 Create `docs/.vitepress/theme/custom.css` with brand color overrides (`--vp-c-brand-1: #2563eb`, `--vp-c-brand-2: #3b82f6`, `--vp-c-brand-3: #60a5fa`)
- [x] 1.3 Import `custom.css` in `docs/.vitepress/theme/index.ts`
- [x] 1.4 Add `themeConfig.logo: '/logo.svg'` to `docs/.vitepress/config.ts`
- [x] 1.5 Update `docs/index.md` hero section to include logo image

## 2. LikeC4 Setup

- [x] 2.1 Add `likec4` and `@leberkas-org/vitepress-likec4` to `docs/package.json`, run `npm install`
- [x] 2.2 Wrap VitePress config with `withLikeC4({ likec4: { source: './likec4' } }, defineConfig({...}))` in `docs/.vitepress/config.ts`
- [x] 2.3 Create `docs/likec4/specification.c4` — element kinds: `externalSystem`, `system`, `container`, `component`, `dataStore`; tags for zones (ingest, domain, egress); color assignments (blue for njord, green for external, amber for pipeline stages)

## 3. LikeC4 Architecture Model

- [x] 3.1 Create `docs/likec4/model.c4` — elements: Open-Meteo API (externalSystem), njord (system) containing Ingest/Domain/Egress containers with key components, MQTT Broker (dataStore), Home Assistant (externalSystem); relationships showing data flow
- [x] 3.2 Create `docs/likec4/views.c4` — three views: `index` (system context, left-to-right), `internals` (container-level showing zones), `pipeline` (component-level showing Akka.Streams stages)
- [x] 3.3 Validate model compiles: `npx likec4 validate` from `docs/`

## 4. Architecture Docs Page

- [x] 4.1 Create `docs/architecture.md` — embed all three LikeC4 views (`<likec4-view view-id="index" />`, `internals`, `pipeline`) with explanatory text about the three-zone design and streaming pipeline
- [x] 4.2 Add "Architecture" link to sidebar in `docs/.vitepress/config.ts` under the "Guide" section
- [x] 4.3 Embed `<likec4-view view-id="index" />` in `docs/getting-started.md` after the "Verify it works" section as a visual overview

## 5. Static SVG Export

- [x] 5.1 Export `index` view as PNG: `npx likec4 export --format svg` → `docs/public/architecture.svg`
- [x] 5.2 Add npm script `"export:diagrams"` to `docs/package.json` for reproducible export

## 6. README Redesign

- [x] 6.1 Rewrite `README.md` with centered logo (`docs/public/logo.svg`), `<h1>` title, tagline, badge row (license, .NET, docs link)
- [x] 6.2 Add feature highlights section (4-6 bullets: multi-model, multi-location, enrichment, MQTT discovery, low resources)
- [x] 6.3 Add minimal quick-start section (docker-compose snippet, under 20 lines)
- [x] 6.4 Add architecture section embedding `docs/public/architecture.svg` as an image
- [x] 6.5 Add documentation link section and license/attribution footer (MIT + Open-Meteo CC BY 4.0)

## 7. Validation

- [x] 7.1 Run `npm run docs:build` from repo root (or `docs/`) — VitePress builds without errors, LikeC4 diagrams compile
- [x] 7.2 Run `npm run docs:dev` and visually verify: logo in navbar, brand colors applied, architecture page with interactive diagrams, hero section with logo
- [x] 7.3 Verify `README.md` renders correctly on GitHub (logo, badges, architecture SVG, formatting)
- [x] 7.4 Verify `docs/public/index.png` matches current LikeC4 index view matches the current LikeC4 `index` view
