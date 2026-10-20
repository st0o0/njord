## Context

The config builder is a Vue component that generates njord configuration. It references the old Models/Horizons/Parameters architecture.

## Goals / Non-Goals

**Goals:** Builder generates valid config for the domain-based architecture.
**Non-Goals:** Full enrichment configuration, API preview.

## Decisions

### D1: Domain checkboxes replace model list
A simple set of checkboxes: Weather (always on), AirQuality, Flood, Marine (opt-in). No model IDs to select.

### D2: Budget formula simplified
`monthly = enabled_domains × locations × 24 × 30`. No per-model weight calculation. Each domain = 1 API call per cycle.

### D3: Location entry is name-only
Input field for location name. Geocoding resolves at runtime. Optional "advanced" toggle for explicit coordinates.
