## ADDED Requirements

### Requirement: Daily values are published on day-offset topics
Daily parameters SHALL be exposed with day-offset horizons (`d0` = today, `d1` = tomorrow, etc.) derived from the effective forecast days of the model. `MqttStatePublisher` SHALL publish one retained state message per day offset to `{baseTopic}/{location}/{model}/d{offset}`, next to the hourly `h{N}` topics. `TopicScheme.DailyUniqueId` SHALL name a daily value `njord_{location}_{model}_{param}_d{offset}` for consumers that build entity ids.

#### Scenario: Day-offset topics
- **WHEN** `forecast_days=4`, `temperature_2m_max` is active and the model delivers 4 days
- **THEN** state messages are published to `.../{model}/d0` through `.../{model}/d3`, each containing `temperature_2m_max`

#### Scenario: Day-offset unique id
- **WHEN** `TopicScheme.DailyUniqueId("lucerne", icon_d2, temperature_2m_max, 1)` is called
- **THEN** it returns `njord_lucerne_icon_d2_temperature_2m_max_d1`

## REMOVED Requirements

### Requirement: Daily sensors use day-offset horizons in HA
**Reason**: The requirement named types, files or behavior that no longer exist; it is restated against the current code.
**Migration**: Superseded by "Daily values are published on day-offset topics" in this capability.
