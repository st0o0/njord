## Context

The njord repo hosts the .NET weather service plus HA integration files inherited from a monorepo merge. The HA integration already exists standalone at `st0o0/ha-njord` with identical code, its own CI/release-please, and a HACS-native layout. The njord docs are VitePress, service-centric, with a single "Home Assistant" page.

## Goals / Non-Goals

**Goals:**

- njord repo contains only .NET service, protos, docs, and tooling — no Python
- Docs present the full njord concept (service + integration + dev workflow)
- Optional: automated proto sync from njord releases to ha-njord

**Non-Goals:**

- Changing any .NET or Python application code
- Rewriting existing docs content — restructure only, reuse existing text
- Making ha-njord depend on njord at build time

## Decisions

### D1: Delete HA files, don't move them

The HA code already lives in ha-njord. Delete `custom_components/`, `ha/`, `hacs.json` from njord. No migration, no git subtree — just remove.

### D2: Single-package release-please

Remove the `ha` package from `release-please-config.json`. Keep only `src`. Remove `ha` from `.release-please-manifest.json`. The `ha-publish` job and all `ha_*` outputs in `release.yml` go away.

### D3: Docs sidebar structure

```
nav:  Guide | Home Assistant | Config | Reference | Dev

sidebar:
  Guide
    Getting Started        ← existing, light edit
    Architecture           ← existing, expand to full system

  Home Assistant
    Installation           ← NEW, from existing home-assistant.md
    Entities               ← NEW
    Options                ← NEW
    Diagnostics            ← NEW

  Configuration            ← existing section, unchanged
    Overview, Locations, Models, Horizons, Parameters,
    Enrichment, MQTT, Persistence, Budget

  Reference                ← regroup existing pages
    Model Catalog
    MQTT Topics
    Config Builder

  Development              ← NEW section
    Dev Setup              ← NEW (docker-compose for both repos)
    Service                ← NEW (brief .NET dev guide)
    Integration            ← NEW (brief Python dev guide, link to ha-njord)
    Proto Management       ← NEW (proto workflow, sync mechanism)
```

### D4: Existing page URL stability

`/getting-started`, `/architecture`, `/configuration/*`, `/models`, `/mqtt-reference`, `/builder` keep their URLs. `/home-assistant` moves to `/home-assistant/` (index). New pages get new URLs under `/home-assistant/` and `/development/`.

### D5: Proto-sync via repository_dispatch (optional)

njord `release.yml` adds a `notify-ha` job after `release-please` that sends a `repository_dispatch` event to `st0o0/ha-njord`. ha-njord adds `sync-protos.yml` that sparse-checkouts `protos/` from njord, copies them, runs `make proto`, and opens a PR via `peter-evans/create-pull-request`. Requires a PAT with `repo` scope stored as `HA_NJORD_PAT` secret in njord.

### D6: HA entity docs sourced from ha-njord knowledge

The new entities page documents what exists in ha-njord — platforms, naming, attributes. Since this is documentation (not code), it lives in njord's docs alongside the rest of the project docs. ha-njord README links to it.

## Risks / Trade-offs

- **Proto drift**: Without the sync workflow, protos could diverge silently. Mitigation: proto-sync is low-effort to implement and creates automated PRs.
- **Doc staleness**: HA entity docs in njord could get out of sync with ha-njord code changes. Mitigation: entity reference is structural (platform names, attribute shapes) and changes rarely; ha-njord README links to the docs.
- **URL changes**: `/home-assistant` moves to `/home-assistant/`. Old links break. Mitigation: VitePress doesn't have redirects out of the box, but the content stays discoverable via nav/sidebar.
