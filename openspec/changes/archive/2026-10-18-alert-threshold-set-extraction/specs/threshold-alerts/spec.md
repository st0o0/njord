## MODIFIED Requirements

### Requirement: Alert thresholds are configurable per alert type
Each alert rule SHALL read its thresholds from `AlertSettings`, which is produced by converting `AlertThresholdValues` (the shared config-layer record) through `AlertSettingsConversion.ToSettings()`. The conversion SHALL transform `IReadOnlyList<double>` multi-tier thresholds into `SeverityThresholds` value objects and pass scalar thresholds through unchanged. `AlertSettings` retains its current 16-parameter shape with `SeverityThresholds` for multi-tier fields.

#### Scenario: AlertSettings produced from shared threshold values
- **WHEN** `AlertThresholdValues` carries frost thresholds `[0, -5, -15]`
- **THEN** `AlertSettings.Frost` SHALL be a `SeverityThresholds` with Yellow=0, Orange=-5, Red=-15

#### Scenario: Scalar thresholds pass through
- **WHEN** `AlertThresholdValues` carries `HeavyRainHourlyThreshold = 10.0`
- **THEN** `AlertSettings.HeavyRainHourlyThreshold` SHALL be `10.0`

#### Scenario: All alert rules continue to receive their thresholds
- **WHEN** the pipeline computes alerts
- **THEN** every alert rule SHALL receive the same threshold values as before, regardless of whether they came through `AlertThresholdValues` or directly from options
