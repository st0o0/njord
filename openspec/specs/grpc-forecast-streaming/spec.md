# grpc-forecast-streaming Specification

## Purpose

Server-streaming gRPC RPC for pushing real-time forecast updates to connected
clients via the EgressActor BroadcastHub. Supports multi-client fan-out and
optional location filtering.

## Requirements

### Requirement: StreamForecasts pushes forecast updates in real-time
`ForecastService.StreamForecasts` SHALL be a server-streaming RPC. It SHALL subscribe to the EgressActor BroadcastHub, filter for `PerModelUpdate` events, map them to `ForecastUpdate` proto messages, and write them to the gRPC response stream. Each connected client SHALL receive all forecast updates for all locations and models.

#### Scenario: Client receives forecast update after poll cycle
- **WHEN** a client calls `StreamForecasts` and njord completes a poll cycle for (lucerne, icon_d2)
- **THEN** the client SHALL receive a `ForecastUpdate` with location "lucerne", model "icon_d2", and hourly/daily forecast points

#### Scenario: Multiple clients receive same update
- **WHEN** two clients have active `StreamForecasts` streams and a poll cycle completes
- **THEN** both clients SHALL receive the same `ForecastUpdate`

#### Scenario: Stream ends on client disconnect
- **WHEN** a client disconnects from the `StreamForecasts` stream
- **THEN** the server-side stream SHALL be disposed and the BroadcastHub subscription released

#### Scenario: Stream filters by location when requested
- **WHEN** a client calls `StreamForecasts` with location "lucerne"
- **THEN** the client SHALL only receive updates for location "lucerne", not other locations
