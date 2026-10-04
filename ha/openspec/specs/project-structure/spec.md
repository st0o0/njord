## Purpose

Defines the foundational project structure for the ha-njord Home Assistant custom integration — Python project configuration, HA integration skeleton, test infrastructure, and HACS compatibility.

## Requirements

### Requirement: Valid HA integration skeleton
The project SHALL contain a `custom_components/njord/` directory with a valid `manifest.json` that Home Assistant Core can discover and load.

#### Scenario: HA discovers the integration
- **WHEN** ha-njord is installed into a Home Assistant instance's `custom_components/` directory
- **THEN** Home Assistant recognizes "njord Weather" as an available integration in the integrations list

#### Scenario: Manifest declares correct metadata
- **WHEN** `manifest.json` is parsed
- **THEN** it contains domain `njord`, iot_class `local_push`, codeowners `["@st0o0"]`, and requirements for `grpcio` and `protobuf`

### Requirement: Python project configuration
The project SHALL have a `pyproject.toml` declaring project metadata, runtime dependencies, and dev dependencies.

#### Scenario: Dev environment setup
- **WHEN** a developer runs `pip install -e ".[dev]"` from the project root
- **THEN** all runtime dependencies (`grpcio`, `protobuf`) and dev dependencies (`pytest`, `grpcio-tools`) are installed

### Requirement: Test infrastructure
The project SHALL contain a `tests/` directory with a `conftest.py` providing shared test fixtures.

#### Scenario: Pytest discovers tests
- **WHEN** `pytest` is run from the project root
- **THEN** pytest discovers the test directory and runs without configuration errors (even if no test files exist yet)

### Requirement: HACS compatibility
The project SHALL include a `hacs.json` file at the project root for HACS discoverability.

#### Scenario: HACS recognizes the integration
- **WHEN** the repository is added as a custom repository in HACS
- **THEN** HACS reads `hacs.json` and lists the integration as installable

### Requirement: Git ignore configuration
The project SHALL include a `.gitignore` file with Python defaults, virtual environment directories, and IDE files excluded.

#### Scenario: Generated artifacts excluded
- **WHEN** a developer runs `git status` after setting up the dev environment
- **THEN** virtual environment directories, `__pycache__`, `.pyc` files, and IDE config are not tracked
