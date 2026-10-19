## REMOVED Requirements

### Requirement: Override file uses section-wrapped JSON
**Reason**: The override file mechanism is removed entirely. Configuration comes from appsettings.json and environment variables only.
**Migration**: Move any overrides from `data/appsettings.Override.json` into environment variables or a mounted appsettings file.

### Requirement: Override file is created on first mutation
**Reason**: No runtime mutations exist. No override file is created or managed.
**Migration**: N/A.

### Requirement: Atomic file write
**Reason**: No override file writes occur.
**Migration**: N/A.

### Requirement: Concurrent mutations are serialized
**Reason**: No runtime mutations exist.
**Migration**: N/A.

### Requirement: Reload triggers IOptionsMonitor change notification
**Reason**: No runtime config changes. No IOptionsMonitor change subscriptions are needed by ConfigManager (which is removed). Options are read once at startup or construction via IOptions<T>.
**Migration**: Components that used IOptionsMonitor for hot settings now use IOptions<T>.
