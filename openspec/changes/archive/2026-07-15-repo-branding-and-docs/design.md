## Context

njord is a fully implemented weather-to-MQTT bridge with comprehensive VitePress
documentation, but no visual identity. The repo uses VitePress (already set up at
`docs/`) with a custom theme that registers a ConfigBuilder Vue component. Two
sibling projects (GaudiHTTP, Matterhorn) have successfully integrated LikeC4
architecture diagrams into VitePress — Matterhorn uses the simplified
`@leberkas-org/vitepress-likec4` plugin wrapper which we will adopt.

Current docs state:
- `docs/.vitepress/config.ts` — VitePress config, references `logo.svg` (missing)
- `docs/.vitepress/theme/index.ts` — custom theme extending default, registers
  ConfigBuilder
- `docs/public/` — no logo or brand assets
- `docs/package.json` — only `vitepress` dependency
- `README.md` — functional but plain, no logo/badges/visual hierarchy

## Goals / Non-Goals

**Goals:**
- Establish a consistent visual identity (logo, colors) across README and docs
- Add interactive LikeC4 architecture diagrams to the documentation
- Make the README a compelling landing page for GitHub visitors
- Ensure all docs content matches the actual implementation

**Non-Goals:**
- Custom VitePress layout components beyond color/logo theming
- Animated or illustrated logo (programmatic SVG only)
- CI/CD pipeline changes (badges will be static/shields.io until CI exists)
- Any changes to `src/` — this is purely a docs/branding change

## Decisions

### D1: Logo — Nordic wind-rose SVG

**Choice**: A geometric, Vegvísir-inspired wind-rose/compass motif as a
monochrome SVG.

**Why over alternatives**:
- *Abstract weather icon* — too generic, doesn't connect to the name "Njord"
- *Full illustration* — requires design tools, hard to maintain, doesn't scale
  to favicon
- *Text-only* — VitePress already shows the project name; a symbol adds identity

The SVG will be a 48×48 viewBox, monochrome (uses `currentColor` for theme
adaptability), with 8 directional rays emanating from a center point — a
simplified compass rose. It works at favicon size (16px) and in the navbar.

### D2: Color palette — cool blue tones

**Choice**: Replace VitePress default purple/violet with a Nordic-cool blue
palette via CSS custom properties.

```
Brand:     #2563eb  (--vp-c-brand-1)
Light:     #3b82f6  (--vp-c-brand-2)
Lighter:   #60a5fa  (--vp-c-brand-3)
Dark bg:   #1e3a5f  (hero gradient accent)
```

**Why**: Blue connects to weather/sky/sea themes and Njord's domain. VitePress
exposes `--vp-c-brand-*` custom properties, making the swap a single CSS block
in `.vitepress/theme/custom.css`.

### D3: LikeC4 integration — plugin wrapper approach

**Choice**: Use `@leberkas-org/vitepress-likec4` (the Matterhorn pattern), not
the manual React-bridge approach (GaudiHTTP pattern).

**Why**:
- Single `withLikeC4()` config wrapper replaces ~100 lines of manual plumbing
  (Vite plugin registration, Vue component, React bridge, ambient types)
- Proven in Matterhorn with the same VitePress version
- Diagrams embed via `<likec4-view view-id="..." />` web component in markdown
- Falls back gracefully in static build (SSG-safe)

**Setup**:
```
docs/
├── likec4/
│   ├── specification.c4   ← element kinds, tags, colors
│   ├── model.c4           ← njord architecture elements
│   └── views.c4           ← diagram views
├── .vitepress/
│   └── config.ts          ← withLikeC4({ source: './likec4' }, defineConfig({...}))
└── package.json           ← + likec4, @leberkas-org/vitepress-likec4
```

### D4: C4 model structure — three views

**Views planned**:

1. **index** (system context) — Open-Meteo API → njord → MQTT Broker → Home
   Assistant. High-level, used in README (static SVG export) and docs hero.
2. **internals** (container) — The three zones: Ingest (OpenMeteoClient,
   Parser), Domain (Forecasts, Enrichment), Egress (MQTT Publisher, Discovery).
   Shows the zone separation guardrail.
3. **pipeline** (component) — The Akka.Streams pipeline flow:
   tick → fan-out → throttle → HTTP → aggregate → enrich → MQTT.

**Element kinds**: `externalSystem` (Open-Meteo, HA), `system` (njord),
`container` (zones), `component` (pipeline stages), `dataStore` (MQTT broker,
SQLite).

### D5: README structure

```
<p align="center"><img src="docs/public/logo.svg" width="120" /></p>
<h1 align="center">njord</h1>
<p align="center">tagline + badges</p>

## Features          (4 bullet highlights)
## Quick Start       (docker compose, 15 lines max)
## Architecture      (static SVG from LikeC4 index view)
## Documentation     (link to VitePress site)
## License           (MIT + Open-Meteo CC BY 4.0 attribution)
```

The architecture diagram in README will be a **static SVG export** from LikeC4
(not the interactive web component) since GitHub markdown doesn't support
custom elements. Export via `npx likec4 export --format svg` into
`docs/public/architecture.svg`, referenced from README as an image.

### D6: Content accuracy audit scope

Review these specific docs pages against the codebase:
- `index.md` hero features — consensus is listed as a feature but is deferred
  per CLAUDE.md decisions; however, the code audit confirmed `ConsensusEnrichment`
  IS implemented, so the hero claim is valid.
- `home-assistant.md` — references consensus and alert entities; both are
  implemented.
- No content changes expected beyond minor wording tweaks if found.

## Risks / Trade-offs

- **[LikeC4 version compatibility]** → Pin `likec4` to the same major version
  as `@leberkas-org/vitepress-likec4` expects. Check Matterhorn's working
  versions as baseline (`likec4@^1.58.0`).
- **[Static SVG export for README may drift]** → Document the export command in
  a `docs/README.md` or package.json script so it can be re-run when the model
  changes.
- **[Logo as currentColor may not render on GitHub]** → GitHub strips some SVG
  attributes. Test the logo renders correctly in GitHub's markdown renderer;
  fall back to a fixed dark color (`#1e293b`) if needed.
