## Context

njord's documentation was written when the project was an Open-Meteo → MQTT bridge. Since then, the codebase has gained a full enrichment pipeline (7 features), SQLite/PostgreSQL persistence, daily forecasts, gRPC, and a health endpoint. The docs content pages (Getting Started, Configuration, etc.) were updated incrementally and are accurate. But the project-level identity texts (taglines, CLAUDE.md decisions) and the Config Builder still reflect the original bridge scope.

The Config Builder (`docs/.vitepress/theme/builder/`) is a Vue 3 SFC embedded in VitePress. It already handles locations (with geocoding), models, horizons, parameters, MQTT basics, budget calculation, and import/export (JSON + env vars + URL hash sharing). The enrichment data (`docs/data/enrichment.json`) is already structured for programmatic consumption but not wired into the builder UI.

## Goals / Non-Goals

**Goals:**
- Update all taglines and descriptions to reflect njord as a weather intelligence service
- Clean up stale CLAUDE.md decisions
- Add Enrichment configuration UI to the Config Builder (driven by enrichment.json)
- Add Persistence provider selection (SQLite/PostgreSQL)
- Add MQTT auth fields (Username/Password) to the builder UI
- Add docker-compose.yml as a third export format
- Expose Discovery Interval in General section

**Non-Goals:**
- Changing any .NET service code
- Rewriting docs content pages (already accurate)
- Adding new data files or modifying models.json/enrichment.json/parameters.json
- Redesigning the builder layout or migrating away from VitePress

## Decisions

### 1. Tagline wording

**Decision**: Use "Multi-model weather intelligence for Home Assistant" as the primary tagline.

**Rationale**: "Intelligence" captures enrichment, alerts, trends, consensus — not just data bridging. "Multi-model" is the key differentiator. Avoids "Open-Meteo" in the tagline since it's an implementation detail (data source), not the product identity.

**Alternatives considered**:
- "Weather forecast processing service" — too generic, sounds like a backend service
- "Open-Meteo weather enrichment for HA" — couples identity to data source

### 2. Enrichment UI: data-driven from enrichment.json

**Decision**: Import `enrichment.json` in ConfigBuilder.vue and render features dynamically — toggle per feature, option inputs per feature's `options` array.

**Rationale**: enrichment.json already defines the feature names, defaults, and option types. Driving the UI from it avoids duplication and stays in sync as features evolve. The serializer already handles `config.enrichment` as `Record<string, Record<string, unknown>>`.

**Alternatives considered**:
- Hardcoded enrichment UI — would drift from data, more maintenance
- Separate EnrichmentBuilder component — unnecessary complexity for 7 toggles with options

### 3. docker-compose.yml export

**Decision**: Add a `exportAsCompose(config)` function in `serializer.ts` that generates a complete `docker-compose.yml` string. The compose format uses environment variables for all config (not a mounted appsettings.json), with a `njord-data` volume for persistence.

**Rationale**: Environment variables are the recommended Docker config approach and match the existing env export. A mounted config file would require the user to also generate and place the JSON — the compose file should be self-contained.

**Template structure**:
```yaml
services:
  njord:
    image: ghcr.io/st0o0/njord:latest
    restart: unless-stopped
    volumes:
      - njord-data:/app/data
    environment:
      - Njord__Mqtt__Host=...
      - Njord__Locations__0__Name=...
      # ... all env vars from exportAsEnvVars()

volumes:
  njord-data:
```

### 4. CLAUDE.md cleanup scope

**Decision**: Update the project description paragraph, remove the consensus-deferral decision, and mark implemented decisions as landed. Keep the Decisions section structure but only retain decisions that are still forward-looking or constraining.

**Rationale**: CLAUDE.md should reflect current architectural constraints, not historical pivots. Decisions that have been fully implemented and are visible in the code don't need to be documented as decisions — they're just how the system works.

## Risks / Trade-offs

- **[Tagline subjectivity]** → The tagline is a creative choice; the user can adjust wording before committing.
- **[enrichment.json coupling]** → Builder UI depends on the enrichment.json shape. If the shape changes, the builder must update. Mitigated by the shape being stable (name + enabledByDefault + options array).
- **[docker-compose version]** → Generated compose file uses the modern format (no `version:` key). Users on Docker Compose v1 would need to add it. Mitigated by Compose v2 being the default for years.
