## REMOVED Requirements

### Requirement: GetStatus returns server health and budget usage
**Reason**: The v1 `njord.v1` protos and services (`ConfigService`, `ForecastService`) no longer exist; only `protos/njord/v2` is built.
**Migration**: `OpsService.GetStatus` and its `StatusResponse`/`ModelStatus` messages in `grpc-v2-ops-service` (timeout and active-enrichment scenarios carried over).

### Requirement: ServerStatus proto includes active_enrichments field
**Reason**: The v1 `njord.v1` protos and services (`ConfigService`, `ForecastService`) no longer exist; only `protos/njord/v2` is built.
**Migration**: `OpsService.GetStatus` and its `StatusResponse`/`ModelStatus` messages in `grpc-v2-ops-service` (timeout and active-enrichment scenarios carried over).

### Requirement: ModelStatus proto reflects poll state fields
**Reason**: The v1 `njord.v1` protos and services (`ConfigService`, `ForecastService`) no longer exist; only `protos/njord/v2` is built.
**Migration**: `OpsService.GetStatus` and its `StatusResponse`/`ModelStatus` messages in `grpc-v2-ops-service` (timeout and active-enrichment scenarios carried over).
