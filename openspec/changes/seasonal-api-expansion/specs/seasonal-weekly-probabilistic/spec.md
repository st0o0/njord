## ADDED Requirements

### Requirement: Weekly EFI variables
The system SHALL support requesting weekly Extreme Forecast Index (EFI) variables from the Seasonal API: `temperature_2m_efi` and `precipitation_efi`. EFI values range from -1 to +1 where values near the extremes indicate high probability of unusual weather.

#### Scenario: Weekly EFI requested
- **WHEN** a feature declares weekly EFI requirements
- **THEN** the Seasonal client SHALL make a separate request with `&weekly=temperature_2m_efi,precipitation_efi`
- **AND** the parsed response SHALL contain EFI values per week

### Requirement: Weekly Shift of Tails variables
The system SHALL support requesting Shift of Tails (SOT) variables: `temperature_2m_sot10`, `temperature_2m_sot90`, `precipitation_sot90`. SOT indicates the shift of the forecast distribution tails relative to climatology.

#### Scenario: Weekly SOT requested
- **WHEN** a feature declares weekly SOT requirements
- **THEN** the Seasonal client SHALL include SOT variables in the weekly request
- **AND** the parsed response SHALL contain SOT values per week

### Requirement: Weekly anomaly probability thresholds
The system SHALL support requesting anomaly probability threshold variables: `temperature_2m_anomaly_gt0`, `temperature_2m_anomaly_gt1`, `temperature_2m_anomaly_gt2`, `temperature_2m_anomaly_ltm1`, `temperature_2m_anomaly_ltm2`, and `precipitation_anomaly_gt0`, `precipitation_anomaly_gt10`, `precipitation_anomaly_gt20`. These give the probability (0-100%) that the anomaly exceeds specific thresholds.

#### Scenario: Anomaly probability thresholds requested
- **WHEN** a feature declares anomaly probability requirements
- **THEN** the Seasonal client SHALL include the threshold variables in the weekly request
- **AND** the parsed response SHALL contain probability percentages per week
