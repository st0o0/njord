## Why

The Vue-based config builder in `docs/.vitepress/theme/builder/` still reflects the old multi-model consensus architecture: it configures Models, Horizons, and per-model parameter groups. The new domain-based architecture uses EnabledDomains, Geocoding for locations, and domain-based budget calculation. Users who generate config from the builder get stale output.

## What Changes

- Replace Models list with EnabledDomains checkboxes (Weather, AirQuality, Flood, Marine).
- Remove Horizons configuration (has no effect in current architecture).
- Update budget calculation: `calls = enabled_domains × locations × cycles_per_day`.
- Location config: Name only (Geocoding resolves coordinates), with optional explicit lat/lon override.
- Update export: appsettings.json and env var output matches new config shape.
- Add Geocoding preview: "Lucerne → 47.05°N, 8.31°E" (optional, can be a later enhancement).

## Non-goals

- Adding enrichment/alert configuration to the builder.
- Interactive API testing.

## Capabilities

### New Capabilities

- `config-builder-domains`: Domain-based configuration builder with updated budget calculation.

### Modified Capabilities

_(none — purely frontend change)_

## Impact

- **docs/.vitepress/theme/builder/**: ConfigBuilder.vue, budget.ts, types.ts, serializer.ts.
- No backend changes.
