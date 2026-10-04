## Purpose

Defines the gRPC client module for communicating with the njord weather service — channel management, unary RPCs, server-streaming RPCs, reconnect logic, and typed data models.

## Requirements

### Requirement: Channel lifecycle
The `NjordClient` SHALL manage a gRPC channel to a njord server specified by host and port, supporting explicit connect, close, and async context manager usage. On connect, it SHALL create four service stubs: `WeatherServiceStub`, `AdminServiceStub`, `OpsServiceStub`, and `SensorServiceStub`.

#### Scenario: Connect and close
- **WHEN** a caller creates a `NjordClient(host, port)` and calls `await client.connect()`
- **THEN** an insecure gRPC channel is opened to `host:port` and four service stubs (WeatherService, AdminService, OpsService, SensorService) are created

#### Scenario: Context manager
- **WHEN** a caller uses `async with NjordClient(host, port) as client:`
- **THEN** the channel is opened on entry and closed on exit

#### Scenario: Close releases resources
- **WHEN** `await client.close()` is called
- **THEN** the gRPC channel is closed and all stubs are set to None

### Requirement: Get catalog
The client SHALL provide an async method to retrieve all locations and deduplicated model info from njord in a single call via `WeatherService.GetCatalog`.

#### Scenario: Successful retrieval
- **WHEN** `await client.get_catalog()` is called
- **THEN** a `CatalogData` object is returned containing a list of `LocationInfo` (name, latitude, longitude, models) and a dict of `ModelInfoData` keyed by model ID

#### Scenario: Replaces get_locations and get_models
- **WHEN** the coordinator needs to discover locations and their models
- **THEN** it calls `get_catalog()` once instead of `get_locations()` + N x `get_models(location)`

### Requirement: Get forecast
The client SHALL provide an async method to retrieve the current forecast for a given location and model, returning typed dataclasses. The `updated_at` field SHALL be a `datetime` converted from `google.protobuf.Timestamp`.

#### Scenario: Successful retrieval
- **WHEN** `await client.get_forecast(location, model)` is called
- **THEN** a `ForecastData` object is returned containing location, model, updated_at as datetime, hourly forecasts, and daily forecasts

#### Scenario: Hourly forecast fields
- **WHEN** a `HourlyForecastData` is inspected
- **THEN** it contains valid_at (datetime), temperature, apparent_temperature, precipitation, humidity, wind_speed, wind_bearing, cloud_cover, weather_code, is_day, rain, wind_gusts, and pressure_msl as optional fields

### Requirement: Get config
The client SHALL provide an async method to retrieve njord's current configuration via `AdminService.GetConfig`.

#### Scenario: Successful retrieval
- **WHEN** `await client.get_config()` is called
- **THEN** a `NjordConfigData` object is returned containing locations (with coordinates and models), default_models, horizons, forecast_days, and poll_interval_seconds

### Requirement: Get status
The client SHALL provide an async method to retrieve njord's server status via `OpsService.GetStatus`.

#### Scenario: Successful retrieval
- **WHEN** `await client.get_status()` is called
- **THEN** a `ServerStatusData` object is returned containing version, uptime_seconds, budget information, process_start (datetime), model_statuses (list of ModelStatusData), and active_enrichments (list of strings)

### Requirement: Trigger poll
The client SHALL provide an async method to trigger a forecast poll via `OpsService.TriggerPoll`.

#### Scenario: Trigger all targets
- **WHEN** `await client.trigger_poll()` is called with no arguments
- **THEN** `OpsService.TriggerPoll(location="", model="")` is called and the response's `triggered_count` is returned

#### Scenario: Trigger specific target
- **WHEN** `await client.trigger_poll(location="graz", model="icon_d2")` is called
- **THEN** `OpsService.TriggerPoll(location="graz", model="icon_d2")` is called

### Requirement: Stream forecasts
The client SHALL provide an async iterator for real-time forecast updates via `WeatherService.StreamForecasts` server-streaming RPC.

#### Scenario: Receive updates
- **WHEN** `async for update in client.stream_forecasts()` is used
- **THEN** each iteration yields a `ForecastData` object when njord pushes a forecast update

#### Scenario: Filter by location
- **WHEN** `client.stream_forecasts(location="lucerne")` is called with a location
- **THEN** only forecast updates for that location are received

### Requirement: Stream config
The client SHALL provide an async iterator for real-time config change notifications via `AdminService.StreamConfig` server-streaming RPC.

#### Scenario: Receive config changes
- **WHEN** `async for config in client.stream_config()` is used
- **THEN** each iteration yields an `NjordConfigData` object when njord's configuration changes

### Requirement: Reconnect with exponential backoff
The streaming methods SHALL automatically reconnect on stream failure using exponential backoff.

#### Scenario: Stream failure and recovery
- **WHEN** a streaming RPC connection drops
- **THEN** the client waits with exponential backoff (1s, 2s, 4s, ... up to 60s max) and reconnects automatically, resuming the async iterator

#### Scenario: Backoff reset on success
- **WHEN** a message is successfully received after reconnection
- **THEN** the backoff delay resets to the initial value (1s)

#### Scenario: Disconnect callback
- **WHEN** a stream disconnects or reconnects
- **THEN** the provided `on_disconnect` and `on_reconnect` callbacks are invoked so callers can react (e.g., mark entities unavailable)

### Requirement: Get enrichments
The client SHALL provide an async method to retrieve enrichment data for a given location, returning typed dataclasses.

#### Scenario: Successful retrieval
- **WHEN** `await client.get_enrichments(location)` is called
- **THEN** an `EnrichmentData` object is returned containing alerts, indices, trends, derived, history, and consensus data for that location

### Requirement: Stream enrichments
The client SHALL provide an async iterator for real-time enrichment updates via `WeatherService.StreamEnrichments` server-streaming RPC.

#### Scenario: Receive updates
- **WHEN** `async for event in client.stream_enrichments()` is used
- **THEN** each iteration yields an `EnrichmentData` object containing only the changed enrichment type (partial payload)

#### Scenario: Filter by location
- **WHEN** `client.stream_enrichments(location="lucerne")` is called with a location
- **THEN** only enrichment updates for that location are received

#### Scenario: Reconnect on failure
- **WHEN** the enrichment stream disconnects
- **THEN** the client reconnects with exponential backoff, same as other streaming RPCs

### Requirement: Typed data models
All public API methods SHALL return typed Python dataclasses, never raw protobuf message objects. Temporal fields SHALL be `datetime` objects, not integer epochs.

#### Scenario: IndexData includes daily scores and sub-models
- **WHEN** `IndexData` is returned as part of enrichment data
- **THEN** it contains today's activity scores as top-level int fields, `frost` as `FrostData | None`, `vpd` as `VpdData | None`, and `forecast` as `list[DayScoreData]`

#### Scenario: IndexData parses DayScoreSet array
- **WHEN** the proto `IndexUpdate` contains `days = [{day_offset: 0, laundry: 85, bbq: 70}, {day_offset: 1, laundry: 72, bbq: 55}]`
- **THEN** `IndexData.laundry` is `85`, `IndexData.bbq` is `70`, and `IndexData.forecast` contains one `DayScoreData(day_offset=1, laundry=72, bbq=55)`

#### Scenario: IndexData parses FrostInfo sub-message
- **WHEN** the proto `IndexUpdate` contains `frost = FrostInfo(hours_until_frost=4, confidence=0.85)`
- **THEN** `IndexData.frost` is `FrostData(hours_until=4, confidence=0.85)`

#### Scenario: IndexData parses VpdInfo sub-message
- **WHEN** the proto `IndexUpdate` contains `vpd = VpdInfo(kpa=0.59, category="optimal")`
- **THEN** `IndexData.vpd` is `VpdData(kpa=0.59, category="optimal")`

#### Scenario: IndexData with no frost/vpd
- **WHEN** the proto `IndexUpdate` has no `frost` or `vpd` fields set
- **THEN** `IndexData.frost` is `None` and `IndexData.vpd` is `None`

#### Scenario: IndexData with empty days array
- **WHEN** the proto `IndexUpdate` has an empty `days` array
- **THEN** `IndexData` has all score fields at `0`, `frost`/`vpd` from sub-messages, and `forecast` is `[]`

#### Scenario: No protobuf leakage
- **WHEN** a consumer uses any `NjordClient` method
- **THEN** the return type is a dataclass from `models.py`, not a `_pb2` generated class

#### Scenario: Timestamps are datetime
- **WHEN** a `ForecastData` or `HourlyForecastData` is returned
- **THEN** `updated_at` and `valid_at` are `datetime` objects with UTC timezone, not integers

#### Scenario: ventilation field renamed to night_ventilation
- **WHEN** the proto `DayScoreSet` contains `night_ventilation = 22`
- **THEN** `IndexData.night_ventilation` is `22`
