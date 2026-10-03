## ADDED Requirements

### Requirement: Accurate project description

The README SHALL describe njord as an Open-Meteo weather API to MQTT bridge for Home Assistant, not reference Kachelmann.

#### Scenario: Reader sees current data source
- **WHEN** a user reads README.md
- **THEN** the document describes Open-Meteo as the data source with no mention of Kachelmann

### Requirement: No unused LFS configuration

The repository SHALL NOT configure Git LFS tracking rules when no binary assets are stored.

#### Scenario: Clean gitattributes
- **WHEN** a contributor inspects `.gitattributes`
- **THEN** only `* text=auto` is present (no `filter=lfs` rules)

### Requirement: Minimal gitignore

The `.gitignore` SHALL only contain entries for tooling and output relevant to the project (.NET, IDE files, project-specific custom entries).

#### Scenario: No irrelevant sections
- **WHEN** a contributor inspects `.gitignore`
- **THEN** there are no entries for Python, Click-Once, NCrunch, StyleCop, TeamCity, MonoDevelop, or Microsoft Fakes

### Requirement: Archive not in working tree

Completed OpenSpec change archives SHALL NOT be tracked in the working tree. They are preserved in git history.

#### Scenario: Archive directory removed and ignored
- **WHEN** a contributor runs `git ls-files openspec/changes/archive/`
- **THEN** no files are listed
- **WHEN** a contributor checks `.gitignore`
- **THEN** `openspec/changes/archive/` is listed

### Requirement: No ghost entries in dockerignore

The `.dockerignore` SHALL NOT reference directories that do not exist in the repository.

#### Scenario: No docs entry
- **WHEN** a contributor inspects `.dockerignore`
- **THEN** there is no `docs` entry

### Requirement: No misleading example configs

Example configuration files SHALL be removed when their content does not apply to the project.

#### Scenario: Slopwatch example removed
- **WHEN** a contributor looks for slopwatch configuration
- **THEN** `.slopwatch/config.json.example` does not exist
