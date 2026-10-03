## Context

njord has 60+ configuration options, a model coverage registry with geographic bounds and forecast horizons, parameter groups with API weight impact, and a budget validation formula. Users currently hand-write JSON configs without tooling support. The codebase already contains all the data needed for an intelligent config builder — it just needs to be exported and presented.

## Goals / Non-Goals

**Goals:**
- Comprehensive documentation covering all config options, models, MQTT topics, and HA integration
- Interactive config builder with live budget feedback
- Export as appsettings.json or env vars, import from both
- Shareable config links via URL hash
- Zero-maintenance static hosting

**Non-Goals:**
- Server-side validation or live njord connection
- Automated codebase-to-docs sync pipeline
- Multi-language support

## Decisions

### D1: VitePress over Docusaurus or plain Vite

**Choice:** VitePress — Vue-native, markdown-first, built-in search, lightweight.

**Alternative:** Docusaurus (React), MkDocs (Python), plain Vite + Vue.

**Rationale:** VitePress allows embedding Vue components directly in markdown pages (`<ConfigBuilder />`), shares the Vue ecosystem the team already knows, and produces small static bundles. Docusaurus would require React; MkDocs can't do interactive components.

### D2: Registry data as static JSON, not generated

**Choice:** Manually export `ModelCoverageRegistry` and `ParameterRegistry` data to `docs/data/*.json` files. The builder imports these at build time.

**Alternative:** Source generator that auto-emits JSON from C# records; or runtime API call.

**Rationale:** The registry data changes rarely (when new models are added). Manual export keeps the docs project decoupled from the .NET build — no dotnet dependency in the docs CI. A simple script or test can verify the JSON stays in sync.

### D3: URL hash for state sharing

**Choice:** Encode builder state as base64-compressed JSON in the URL fragment (`#config=eyJ...`). Load the hash on page init to restore state.

**Rationale:** No backend, no database, no auth — just a shareable URL. The fragment is never sent to the server, so there are no length limits from the server side. Modern browsers support fragments of 2KB+ which is enough for any njord config.

### D4: Dual export format (JSON + env vars)

**Choice:** Two export modes with copy-to-clipboard: structured JSON for `appsettings.json`, flat key=value for Docker/compose env vars.

**Rationale:** Docker users set config via environment variables; bare-metal users edit `appsettings.json`. Both need first-class support. The env var format uses .NET's `__` separator convention so values work directly in `docker run -e` or `docker-compose.yml`.

### D5: Import parses both formats

**Choice:** A single "Import" textarea that auto-detects format: if the input starts with `{`, parse as JSON; otherwise parse as `KEY=VALUE` lines.

**Rationale:** Users shouldn't need to know which format they're pasting. Auto-detection is trivial and eliminates a format-selector step.

### D6: Site at docs/ in the repo, not a separate repo

**Choice:** VitePress project lives at `docs/` in the njord repo root.

**Rationale:** Keeps docs close to code. Changes to config options or models can update docs in the same PR. GitHub Actions can build both the .NET project and the docs site from the same repo.

## Risks / Trade-offs

**[Registry data drift]** → If models are added to `ModelCoverageRegistry.cs` but not to `docs/data/models.json`, the builder shows stale data. Mitigation: add a test that compares the registry to the JSON file and fails if they diverge.

**[Budget formula drift]** → If `NjordOptionsValidator` changes its budget formula, the builder's JS implementation may diverge. Mitigation: document the formula in one place (the config reference page) and reference it from both the C# code and the JS code.

**[URL hash size]** → A config with many locations and models could exceed practical URL lengths for sharing. Mitigation: compress the JSON before base64-encoding; if still too long, show a "config too large to share via link" message.
