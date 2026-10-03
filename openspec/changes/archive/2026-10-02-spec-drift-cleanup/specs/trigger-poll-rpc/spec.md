## REMOVED Requirements

### Requirement: TriggerPoll RPC on ConfigService
**Reason**: The v1 `njord.v1` protos and services (`ConfigService`, `ForecastService`) no longer exist; only `protos/njord/v2` is built.
**Migration**: `OpsService.TriggerPoll` in `grpc-v2-ops-service`.

### Requirement: TriggerPoll proto messages
**Reason**: The v1 `njord.v1` protos and services (`ConfigService`, `ForecastService`) no longer exist; only `protos/njord/v2` is built.
**Migration**: `OpsService.TriggerPoll` in `grpc-v2-ops-service`.

### Requirement: SchedulerActor accepts TriggerImmediatePoll message
**Reason**: The v1 `njord.v1` protos and services (`ConfigService`, `ForecastService`) no longer exist; only `protos/njord/v2` is built.
**Migration**: Moved unchanged in substance to `poll-scheduler` (see its ADDED requirement).
