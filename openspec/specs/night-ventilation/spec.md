# night-ventilation Specification

## Purpose

Nighttime ventilation scoring. Replaces the former 24h Ventilation scorer with a nighttime-only variant that uses means computed from nighttime hours (`is_day` <= 0.5). The formula is identical to the former Ventilation scorer; only the input data changes.

## Requirements

### Requirement: NightVentilation score from nighttime outdoor-indoor delta, humidity, wind, rain
`IndexScorer.NightVentilation` SHALL accept mean outdoor temperature (deg C), indoor temperature, mean humidity (%), mean wind speed (m/s), mean precipitation probability (%), and a `ResolvedPreferences`. It SHALL return an `int` score 0-100. High score = open the windows tonight.

The formula SHALL be identical to the former `Ventilation` scorer: temp-delta 0.30, humidity 0.25, wind 0.25, rain 0.20. The indoor temperature SHALL be resolved using the same fallback chain as the former Ventilation: (1) live `IndoorTemperature` from `SensorSnapshot`, (2) configured `ResolvedPreferences.IndoorTemp`, (3) hardcoded default 22.0.

The only difference from the former Ventilation is the input data: means are computed from nighttime hours only (`is_day` <= 0.5) rather than all hours.

#### Scenario: Cool summer night with breeze
- **WHEN** nighttime outdoor mean is 17 deg C, humidity 45%, wind 3 m/s, rain prob 0%, IndoorTemp 22 deg C, all sensitivities 1.0
- **THEN** the score SHALL be >= 85

#### Scenario: Warm tropical night
- **WHEN** nighttime outdoor mean is 26 deg C, humidity 80%, wind 1 m/s, rain prob 0%, IndoorTemp 24 deg C, all sensitivities 1.0
- **THEN** the score SHALL be <= 25

#### Scenario: Rainy night
- **WHEN** nighttime outdoor mean is 15 deg C, humidity 60%, wind 4 m/s, rain prob 70%, IndoorTemp 22 deg C, all sensitivities 1.0
- **THEN** the score SHALL be <= 40

#### Scenario: Live sensor value used for NightVentilation
- **WHEN** the SensorSnapshot contains `IndoorTemperature = 24.5`
- **AND** the config `IndoorTemp` is `22.0`
- **THEN** the NightVentilation score SHALL be computed with indoor temperature `24.5`
