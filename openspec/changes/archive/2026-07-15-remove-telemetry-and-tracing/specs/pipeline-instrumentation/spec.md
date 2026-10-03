## REMOVED Requirements

### Requirement: HTTP fetch spans are traced
**Reason**: OpenTelemetry tracing removed; no span backend.
**Migration**: None — actors retain `ILogger` for operational visibility.

### Requirement: Fetch count is metered
**Reason**: OpenTelemetry metrics removed; no metrics backend.
**Migration**: None.

### Requirement: Fetch duration is metered
**Reason**: OpenTelemetry metrics removed; no metrics backend.
**Migration**: None.

### Requirement: Fetch failures are metered
**Reason**: OpenTelemetry metrics removed; no metrics backend.
**Migration**: None.

### Requirement: Poll attempts are metered
**Reason**: OpenTelemetry metrics removed; no metrics backend.
**Migration**: None.

### Requirement: Data changes are metered
**Reason**: OpenTelemetry metrics removed; no metrics backend.
**Migration**: None.

### Requirement: MQTT publish spans are traced
**Reason**: OpenTelemetry tracing removed; no span backend.
**Migration**: None.

### Requirement: MQTT publishes are metered
**Reason**: OpenTelemetry metrics removed; no metrics backend.
**Migration**: None.

### Requirement: MQTT publish duration is metered
**Reason**: OpenTelemetry metrics removed; no metrics backend.
**Migration**: None.

### Requirement: Discovery publishes are metered
**Reason**: OpenTelemetry metrics removed; no metrics backend.
**Migration**: None.

### Requirement: MQTT connection state is metered
**Reason**: OpenTelemetry metrics removed; no metrics backend.
**Migration**: None.

### Requirement: MQTT reconnects are metered
**Reason**: OpenTelemetry metrics removed; no metrics backend.
**Migration**: None.
