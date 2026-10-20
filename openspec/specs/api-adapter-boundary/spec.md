# api-adapter-boundary Specification

## Purpose
Keeps the gRPC API a thin adapter: it translates requests into messages to the actor
system and message results into proto responses, and holds no state, configuration access
or domain logic of its own.
## Requirements
### Requirement: gRPC responses are unchanged by the boundary refactor
For the same system state, every RPC SHALL return the same response as before this change,
including error status codes and messages.

#### Scenario: Unknown location keeps its status code
- **WHEN** a client calls `WeatherService/GetForecast` for an unconfigured location
- **THEN** the call fails with the same status code and message as before

#### Scenario: Analysis responses unchanged
- **WHEN** the golden-master fixture state is queried through `GetEnrichments`
- **THEN** the response equals the approved golden master

### Requirement: Actor unavailability maps to a gRPC status
When the target actor cannot be resolved or does not answer within the ask timeout, the RPC
SHALL fail with `Unavailable` (resolution) or `DeadlineExceeded` (timeout), and SHALL NOT
leak an unhandled exception.

#### Scenario: Read model not ready
- **WHEN** a unary forecast RPC is called before the forecast read model is resolvable
- **THEN** the call fails with status `Unavailable`

