## Why

The njord monorepo currently hosts both the .NET weather service and the Home Assistant custom integration (Python). This creates structural friction: `custom_components/njord/` must live in the repo root (HACS constraint) while tests and config live in `ha/`, mixing two ecosystems' concerns in one repo. The HA integration already exists as a standalone repo (`ha-njord`) with its own CI, release-please, and HACS-native structure. The monorepo gains little from bundling them — proto changes are rare and easily synced — while paying for mixed root-level config, dual-ecosystem CI, and a `ha/` folder that can't contain the code it tests.

Separately, the VitePress documentation is service-centric: it explains configuration and MQTT topics but doesn't present njord as a complete system (service + integration) or help contributors set up a dev environment.

## What Changes

- **BREAKING**: Remove all HA integration artifacts from the njord repo (`custom_components/`, `ha/`, `hacs.json`, `brand/`)
- Remove Python CI jobs (`python-test`, `python-lint`) and HACS validation from `.github/workflows/ci.yml`
- Remove `ha-publish` job and `ha` package from release-please config; simplify to single-package release
- Restructure VitePress docs: add dedicated Home Assistant section (installation, entities, options), development guide (dev setup for both repos, proto management), and reference section
- Rewrite landing page and architecture page to present the full njord concept (service + integration), not just the service
- Optional: add `notify-ha` job to njord release workflow that dispatches `proto-sync` event to ha-njord; add `sync-protos.yml` workflow in ha-njord that creates a PR with updated proto stubs

## Non-goals

- Changing any HA integration code or tests (those live in ha-njord now)
- Migrating ha-njord's OpenSpec change archive into njord
- Changing the proto file format or gRPC API surface
- Adding new documentation content beyond what restructuring requires (e.g., no new tutorials)

## Capabilities

### New Capabilities

- `repo-cleanup`: Remove HA integration files, simplify CI and release-please to single .NET package
- `docs-restructure`: Reorganize VitePress docs from service-only to full-concept documentation with HA, development, and reference sections
- `proto-sync-workflow`: Optional automated proto synchronization from njord releases to ha-njord via GitHub Actions repository_dispatch

### Modified Capabilities

_(none — no existing spec-level requirements change)_

## Impact

- **Repo structure**: `custom_components/`, `ha/`, `hacs.json` deleted from njord
- **CI**: `.github/workflows/ci.yml` loses Python and HACS jobs; `.github/workflows/labeler.yml` may need path updates
- **Release**: `release-please-config.json` and `.release-please-manifest.json` simplified to single package; `release.yml` loses `ha-publish` job
- **Docs**: `docs/` sidebar, nav, and page structure change significantly; existing page URLs may change (architecture, home-assistant move to subdirectories)
- **Cross-repo**: If proto-sync is implemented, njord release workflow gains a `notify-ha` job and ha-njord gains a `sync-protos.yml` workflow; requires a PAT secret in njord
