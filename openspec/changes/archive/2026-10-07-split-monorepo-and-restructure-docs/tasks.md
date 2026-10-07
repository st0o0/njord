## 1. Repo cleanup

- [x] 1.1 Delete `custom_components/` directory
- [x] 1.2 Delete `ha/` directory
- [x] 1.3 Delete `hacs.json`
- [x] 1.4 Remove `python-test`, `python-lint` jobs from `.github/workflows/ci.yml`
- [x] 1.5 Remove `hacs` job from `.github/workflows/ci.yml`
- [x] 1.6 Remove `ha` package from `release-please-config.json` (keep only `src`)
- [x] 1.7 Remove `ha` entry from `.release-please-manifest.json` (keep only `src`)
- [x] 1.8 Remove `ha-publish` job and all `ha_*` outputs from `.github/workflows/release.yml`
- [x] 1.9 Remove HA path rules from `.github/workflows/labeler.yml` if present (no local config — uses reusable workflow)
- [x] 1.10 Update `CLAUDE.md` and `AGENTS.md`: remove HA integration references, test counts, `ha/` paths, Python conventions
- [x] 1.11 Update `README.md`: remove HA integration install/setup, link to ha-njord repo instead
- [x] 1.12 Update `.gitignore`: remove Python-specific entries (`.ruff_cache/`, `__pycache__/`) if no longer needed
- [x] 1.13 Verify `dotnet build src/Njord.slnx` still passes after cleanup

## 2. Docs restructure

- [x] 2.1 Create `docs/home-assistant/` directory with `index.md` (installation + setup, migrated from existing `home-assistant.md`)
- [x] 2.2 Create `docs/home-assistant/entities.md` (weather, sensor, binary_sensor, event, button platforms with naming and attributes)
- [x] 2.3 Create `docs/home-assistant/options.md` (options flow, enrichment toggles, sensor push, poll interval)
- [x] 2.4 Create `docs/home-assistant/diagnostics.md` (diagnostics download)
- [x] 2.5 Delete old `docs/home-assistant.md` (content moved to `docs/home-assistant/index.md`)
- [x] 2.6 Create `docs/development/` directory with `setup.md` (docker-compose for HA + njord, clone both repos, run full stack)
- [x] 2.7 Create `docs/development/service.md` (brief .NET dev guide: build, test, run)
- [x] 2.8 Create `docs/development/integration.md` (brief Python dev guide, link to ha-njord)
- [x] 2.9 Create `docs/development/protos.md` (proto workflow, `make proto`, sync mechanism)
- [x] 2.10 Update `docs/index.md` hero and features to present full njord concept (service + integration)
- [x] 2.11 Update `docs/architecture.md` to cover end-to-end flow including HA integration, reference ha-njord
- [x] 2.12 Update `docs/.vitepress/config.ts` sidebar and nav to new structure (Home Assistant group, Reference group, Development group)
- [x] 2.13 Move model catalog, MQTT topics, config builder into Reference group in sidebar (no URL changes, just sidebar reorganization)

## 3. Proto-sync workflow (optional)

- [x] 3.1 Add `notify-ha` job to `.github/workflows/release.yml` that dispatches `proto-sync` event to `st0o0/ha-njord` after njord release
- [x] 3.2 Create `.github/workflows/sync-protos.yml` in ha-njord repo: sparse-checkout njord protos, copy, run `make proto`, create PR via `peter-evans/create-pull-request`
- [x] 3.3 Document PAT secret setup (`HA_NJORD_PAT`) in `docs/development/protos.md`
