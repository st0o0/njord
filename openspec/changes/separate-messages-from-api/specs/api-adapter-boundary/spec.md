## Purpose

Keeps the gRPC API a thin adapter: it translates requests into messages to the actor
system and message results into proto responses, and holds no state, configuration access
or domain logic of its own.

## ADDED Requirements

### Requirement: The gRPC API reaches the system only through messages
Every gRPC RPC SHALL obtain domain data and perform state changes only by sending messages
defined in `Njord.Messages` to actors resolved by their marker interface, or by consuming a
stream obtained through such a message. gRPC services SHALL NOT read configuration options,
write configuration, call domain calculators or registries, or host actors.

#### Scenario: Config read goes through the configuration manager
- **WHEN** a client calls `AdminService/GetConfig`
- **THEN** the service asks the configuration manager with a config query and maps the
  returned config snapshot to `NjordConfig`

#### Scenario: Budget status goes through the budget tracker
- **WHEN** a client calls the ops RPC that reports budget usage
- **THEN** the service asks the budget tracker for the budget status (usage and effective
  budget) and does not compute the effective budget itself

#### Scenario: Catalog goes through the configuration manager
- **WHEN** a client calls `WeatherService/GetCatalog`
- **THEN** the service asks for the catalog (locations, models, model coverage) and maps
  the result, without reading options or the coverage registry

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
