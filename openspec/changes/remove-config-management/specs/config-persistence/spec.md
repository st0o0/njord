## REMOVED Requirements

### Requirement: Config mutations persist to override file
**Reason**: The override file mechanism and all runtime config mutations are removed. Configuration is static.
**Migration**: Use environment variables or appsettings.json file mounts for persistent configuration. Restart the container after changes.

### Requirement: Override file overrides environment variables
**Reason**: No override file exists. Environment variables and appsettings.json are the only configuration sources, with standard .NET precedence (env vars override file values).
**Migration**: Environment variables now have the highest precedence (standard .NET behavior). Adjust configuration layering accordingly.

### Requirement: Config mutations trigger IOptionsMonitor change notification
**Reason**: No runtime config mutations exist. ConfigManager is removed.
**Migration**: N/A.

### Requirement: Concurrent mutations are serialized
**Reason**: No runtime mutations exist.
**Migration**: N/A.

### Requirement: Corrupt override reload keeps the last good configuration
**Reason**: No override file and no runtime reloads.
**Migration**: N/A.
