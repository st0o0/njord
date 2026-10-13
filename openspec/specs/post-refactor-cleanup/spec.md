## Purpose

Defines cleanup after the refactoring: removal of ghost directories of deleted projects and version alignment of all Akka packages.

## Requirements

### Requirement: No ghost directories from previous refactoring

Directories that contained projects which no longer exist SHALL be
deleted from the working tree.

#### Scenario: Njord.Ingest removed
- **WHEN** inspecting `src/`
- **THEN** neither `Njord.Ingest/` nor `Njord.Ingest.Tests/` SHALL exist

### Requirement: All Akka packages are version-aligned

All `Akka.*` packages in `Directory.Packages.props` SHALL use the same
patch version where available.

#### Scenario: Akka.Persistence.Sql.Hosting aligned
- **WHEN** the Akka package versions are checked
- **THEN** `Akka.Persistence.Sql.Hosting` SHALL match the version of other Akka packages (currently 1.5.73)
