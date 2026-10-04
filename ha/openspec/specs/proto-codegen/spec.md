## Purpose

Defines how njord's protobuf/gRPC definitions are managed, compiled to Python stubs, and shipped with the integration.

## Requirements

### Requirement: Proto source files present
The project SHALL contain copies of njord's v2 proto definitions at `protos/njord/v2/common.proto`, `protos/njord/v2/weather.proto`, `protos/njord/v2/admin.proto`, and `protos/njord/v2/ops.proto`.

#### Scenario: Proto files are present
- **WHEN** a developer inspects the `protos/njord/v2/` directory
- **THEN** all four proto files (`common.proto`, `weather.proto`, `admin.proto`, `ops.proto`) are present and match njord's current v2 definitions

### Requirement: Python stub generation
The project SHALL provide a Makefile target `make proto` that generates Python gRPC stubs from the v2 proto source files using `grpcio-tools`, correctly resolving `common.proto` imports.

#### Scenario: Successful codegen
- **WHEN** a developer runs `make proto` from the project root
- **THEN** `custom_components/njord/proto/njord/v2/` contains `common_pb2.py`, `weather_pb2.py`, `weather_pb2_grpc.py`, `admin_pb2.py`, `admin_pb2_grpc.py`, `ops_pb2.py`, and `ops_pb2_grpc.py`

#### Scenario: Generated stubs are importable
- **WHEN** a Python script imports `custom_components.njord.proto.njord.v2.weather_pb2`
- **THEN** the import succeeds and the module contains the expected message classes (`GetCatalogRequest`, `GetForecastResponse`, `ForecastUpdate`, etc.)

#### Scenario: Common imports resolve
- **WHEN** `weather_pb2` is imported
- **THEN** its references to `common_pb2` types (`HourlyForecast`, `LocationInfo`, `ModelInfo`, etc.) resolve correctly

### Requirement: Generated stubs committed
The generated Python stubs SHALL be committed to git so that end users installing via HACS do not need `grpcio-tools` or a build step.

#### Scenario: Clean install without build tools
- **WHEN** a user installs ha-njord via HACS (copies `custom_components/njord/` to their HA instance)
- **THEN** all proto-generated modules are present and importable without running codegen
