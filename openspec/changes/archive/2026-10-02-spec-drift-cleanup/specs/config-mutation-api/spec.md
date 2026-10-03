## REMOVED Requirements

### Requirement: AddLocation creates a new location with budget validation
**Reason**: The v1 `njord.v1` protos and services (`ConfigService`, `ForecastService`) no longer exist; only `protos/njord/v2` is built.
**Migration**: Replaced by the declarative `SetLocations`, `SetSettings`, `SetEnrichment`, `SetBudget` RPCs in `grpc-v2-admin-service`; the budget warning behavior is carried over to its `ConfigResponse` requirement.

### Requirement: RemoveLocation removes a location and its data
**Reason**: The v1 `njord.v1` protos and services (`ConfigService`, `ForecastService`) no longer exist; only `protos/njord/v2` is built.
**Migration**: Replaced by the declarative `SetLocations`, `SetSettings`, `SetEnrichment`, `SetBudget` RPCs in `grpc-v2-admin-service`; the budget warning behavior is carried over to its `ConfigResponse` requirement.

### Requirement: UpdateLocation modifies an existing location
**Reason**: The v1 `njord.v1` protos and services (`ConfigService`, `ForecastService`) no longer exist; only `protos/njord/v2` is built.
**Migration**: Replaced by the declarative `SetLocations`, `SetSettings`, `SetEnrichment`, `SetBudget` RPCs in `grpc-v2-admin-service`; the budget warning behavior is carried over to its `ConfigResponse` requirement.

### Requirement: UpdateForecastSettings changes forecast configuration
**Reason**: The v1 `njord.v1` protos and services (`ConfigService`, `ForecastService`) no longer exist; only `protos/njord/v2` is built.
**Migration**: Replaced by the declarative `SetLocations`, `SetSettings`, `SetEnrichment`, `SetBudget` RPCs in `grpc-v2-admin-service`; the budget warning behavior is carried over to its `ConfigResponse` requirement.

### Requirement: UpdateEnrichmentConfig changes enrichment settings
**Reason**: The v1 `njord.v1` protos and services (`ConfigService`, `ForecastService`) no longer exist; only `protos/njord/v2` is built.
**Migration**: Replaced by the declarative `SetLocations`, `SetSettings`, `SetEnrichment`, `SetBudget` RPCs in `grpc-v2-admin-service`; the budget warning behavior is carried over to its `ConfigResponse` requirement.

### Requirement: UpdateBudget overrides the request budget
**Reason**: The v1 `njord.v1` protos and services (`ConfigService`, `ForecastService`) no longer exist; only `protos/njord/v2` is built.
**Migration**: Replaced by the declarative `SetLocations`, `SetSettings`, `SetEnrichment`, `SetBudget` RPCs in `grpc-v2-admin-service`; the budget warning behavior is carried over to its `ConfigResponse` requirement.

### Requirement: Every mutation returns ConfigResponse with budget projection
**Reason**: The v1 `njord.v1` protos and services (`ConfigService`, `ForecastService`) no longer exist; only `protos/njord/v2` is built.
**Migration**: Replaced by the declarative `SetLocations`, `SetSettings`, `SetEnrichment`, `SetBudget` RPCs in `grpc-v2-admin-service`; the budget warning behavior is carried over to its `ConfigResponse` requirement.
