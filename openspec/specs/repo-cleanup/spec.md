# repo-cleanup Specification

## Purpose

Keeps the njord repo scoped to the .NET weather service only: no Home Assistant integration artifacts, Python CI jobs, or HA release packaging — the HA integration lives in its own `ha-njord` repo.

## Requirements

### Requirement: HA integration artifacts removed from njord repo

The njord repo SHALL NOT contain any Home Assistant integration files. The following paths SHALL be deleted: `custom_components/`, `ha/`, `hacs.json`.

#### Scenario: HA directories absent after cleanup

- **WHEN** the cleanup is applied
- **THEN** `custom_components/`, `ha/`, and `hacs.json` do not exist in the repo

### Requirement: CI contains only .NET jobs

The CI workflow (`.github/workflows/ci.yml`) SHALL contain only the `dotnet` and `commits` jobs. All Python test, Python lint, and HACS validation jobs SHALL be removed.

#### Scenario: CI workflow has no Python or HACS jobs

- **WHEN** a PR is opened after cleanup
- **THEN** only `dotnet` and `commits` CI jobs run

### Requirement: Release-please is single-package

`release-please-config.json` SHALL contain only the `src` package. The `ha` package and its `extra-files` entry SHALL be removed. `.release-please-manifest.json` SHALL contain only `"src"`.

#### Scenario: Release creates only njord tag

- **WHEN** a release-please PR is merged
- **THEN** only `njord-v*` tags are created, no `ha-v*` tags

### Requirement: Release workflow has no HA jobs

`release.yml` SHALL NOT contain the `ha-publish` job or produce `njord.zip` archives. The `release-please` job outputs SHALL NOT include `ha_*` outputs.

#### Scenario: Release workflow runs docker and docs only

- **WHEN** a njord release is created
- **THEN** only `docker` and `docs` jobs run after `release-please`

### Requirement: Labeler workflow updated

If `.github/workflows/labeler.yml` references `ha/` or `custom_components/` paths, those label rules SHALL be removed.

#### Scenario: Labeler has no HA path rules

- **WHEN** the labeler config is checked after cleanup
- **THEN** no label rules reference `ha/` or `custom_components/`
