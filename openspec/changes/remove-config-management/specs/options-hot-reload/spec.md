## REMOVED Requirements

### Requirement: Settings are classified as hot or restart-required
**Reason**: Runtime config management is removed. All configuration is static — loaded at startup from appsettings.json and environment variables. There is no hot-reload mechanism and no ConfigManager to propagate changes.
**Migration**: All options are now restart-required. Change configuration by editing appsettings.json or environment variables and restarting the container.

### Requirement: Restart-required changes are reported
**Reason**: No runtime config mutation RPCs exist. There is nothing to report a restart notice for.
**Migration**: N/A — all config changes inherently require restart.

### Requirement: Invalid runtime updates are rejected before persisting
**Reason**: No runtime config mutations exist. Startup validation (ValidateOnStart) remains the sole validation gate.
**Migration**: Invalid configuration is caught at startup. No runtime rejection path needed.
