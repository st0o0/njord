## REMOVED Requirements

### Requirement: AdminService definition
**Reason**: The entire AdminService is removed. Runtime configuration management via gRPC is no longer supported. Configuration is static (appsettings.json + environment variables).
**Migration**: ha-njord must remove its config UI. Operators configure njord by editing docker-compose environment variables and restarting.

### Requirement: GetConfig returns current configuration
**Reason**: Removed with AdminService. Read-only config status is available via OpsService.GetStatus if needed.
**Migration**: Use OpsService for operational status. For configuration inspection, read the container's environment or appsettings.json directly.

### Requirement: StreamConfig pushes config changes
**Reason**: No runtime config changes occur, so there is nothing to stream.
**Migration**: N/A.

### Requirement: SetSettings consolidates forecast settings
**Reason**: Removed with AdminService.
**Migration**: Set forecast settings via environment variables (e.g. `Njord__Polling__PollInterval`, `Njord__Horizons__0`).

### Requirement: IndexConfig message
**Reason**: Removed with AdminService. Index options are configured via appsettings.json or environment variables.
**Migration**: Use `Njord__Enrichment__Indices__*` environment variables.

### Requirement: SetBudget updates budget override
**Reason**: Removed with AdminService.
**Migration**: Use `Njord__Polling__BudgetOverride__RequestsPerMonth` and `__RequestsPerMinute` environment variables.
