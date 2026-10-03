## REMOVED Requirements

### Requirement: GetTriggerTargets RPC on ConfigService
**Reason**: The v1 `njord.v1` protos and services (`ConfigService`, `ForecastService`) no longer exist; only `protos/njord/v2` is built.
**Migration**: `OpsService.GetTargets` in `grpc-v2-ops-service` (poll-state scenario carried over).

### Requirement: TriggerTarget proto message uses Timestamp types
**Reason**: The v1 `njord.v1` protos and services (`ConfigService`, `ForecastService`) no longer exist; only `protos/njord/v2` is built.
**Migration**: `OpsService.GetTargets` in `grpc-v2-ops-service` (poll-state scenario carried over).
