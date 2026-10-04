# Capability: Sensor Units & Precision

## Purpose

Defines device_class, native unit constants, and display precision for all numeric sensor entities so that Home Assistant can auto-convert values to the user's preferred unit system.

## Requirements

### Requirement: Temperature sensors have TEMPERATURE device class
All sensor entities that report temperature values (frost alert, heat alert, diurnal amplitude, model performance) SHALL set `device_class = SensorDeviceClass.TEMPERATURE` and `native_unit_of_measurement = UnitOfTemperature.CELSIUS`.

#### Scenario: Frost alert converts to Fahrenheit for imperial users
- **WHEN** a user has HA unit system set to "US Customary" and the frost alert trigger_value is -2.0°C
- **THEN** HA displays the value as 28.4°F

#### Scenario: Diurnal amplitude shows in user's temperature unit
- **WHEN** a user has HA unit system set to "US Customary" and diurnal amplitude is 8.5°C
- **THEN** HA displays the value converted to Fahrenheit

### Requirement: Wind speed sensors have WIND_SPEED device class
Sensor entities that report wind speed values (storm alert) SHALL set `device_class = SensorDeviceClass.WIND_SPEED` and `native_unit_of_measurement = UnitOfSpeed.KILOMETERS_PER_HOUR`.

#### Scenario: Storm alert converts to mph for imperial users
- **WHEN** a user has HA unit system set to "US Customary" and the storm alert trigger_value is 80 km/h
- **THEN** HA displays the value as approximately 49.7 mph

### Requirement: Pressure sensors have PRESSURE device class
Sensor entities that report pressure values (pressure_drop alert) SHALL set `device_class = SensorDeviceClass.PRESSURE` and `native_unit_of_measurement = UnitOfPressure.HPA`.

#### Scenario: Pressure drop alert converts to inHg for imperial users
- **WHEN** a user has HA unit system set to "US Customary" and the pressure_drop alert trigger_value is 5.0 hPa
- **THEN** HA displays the value converted to inHg

### Requirement: Distance sensors have DISTANCE device class
Sensor entities that report distance values (fog alert, snow alert) SHALL set `device_class = SensorDeviceClass.DISTANCE` with the appropriate HA unit constant (`UnitOfLength.METERS` for fog, `UnitOfLength.CENTIMETERS` for snow).

#### Scenario: Fog alert converts to miles for imperial users
- **WHEN** a user has HA unit system set to "US Customary" and the fog alert trigger_value is 200 m
- **THEN** HA displays the value converted to the imperial distance unit

#### Scenario: Snow alert converts for imperial users
- **WHEN** a user has HA unit system set to "US Customary" and the snow alert trigger_value is 15 cm
- **THEN** HA displays the value converted to inches

### Requirement: Frost hours sensor has DURATION device class
The frost hours sensor SHALL set `device_class = SensorDeviceClass.DURATION` and `native_unit_of_measurement = UnitOfTime.HOURS`.

#### Scenario: Frost hours shows with duration semantics
- **WHEN** enrichment data contains frost_hours = 4
- **THEN** the sensor shows 4 h with device_class DURATION

### Requirement: Non-convertible sensors keep raw string units
Sensor entities whose units have no matching HA device class SHALL keep raw string units without device_class. This includes: UV alert (`"UV"`), thunderstorm alert (`"J/kg"`), VPD (`"kPa"`), all index sensors (`"%"`), COP estimate, API budget, and uptime.

#### Scenario: UV alert stays as-is for all unit systems
- **WHEN** a user has any HA unit system and the UV alert trigger_value is 8
- **THEN** HA displays 8 UV with no conversion

### Requirement: ALERT_UNITS uses HA unit constants where applicable
The `ALERT_UNITS` dict SHALL use HA `UnitOf*` constants for alert types that have a matching device class, and raw strings only for those that do not.

#### Scenario: ALERT_UNITS contains HA constants
- **WHEN** the integration code references `ALERT_UNITS["frost"]`
- **THEN** the value is `UnitOfTemperature.CELSIUS`, not the raw string `"°C"`

### Requirement: All numeric sensors set suggested_display_precision
Every numeric sensor entity SHALL set `_attr_suggested_display_precision` with a value appropriate to the measurement type.

#### Scenario: Temperature sensors show 1 decimal
- **WHEN** the frost alert trigger_value is -2.37°C
- **THEN** HA suggests displaying as -2.4°C (precision 1)

#### Scenario: Pressure sensors show 0 decimals
- **WHEN** the pressure_drop alert trigger_value is 5.23 hPa
- **THEN** HA suggests displaying as 5 hPa (precision 0)

#### Scenario: Index sensors show 0 decimals
- **WHEN** the BBQ Index value is 72
- **THEN** HA suggests displaying as 72% (precision 0)

#### Scenario: VPD shows 2 decimals
- **WHEN** VPD is 1.234 kPa
- **THEN** HA suggests displaying as 1.23 kPa (precision 2)

#### Scenario: COP shows 1 decimal
- **WHEN** COP estimate is 3.47
- **THEN** HA suggests displaying as 3.5 (precision 1)

#### Scenario: User overrides precision in HA UI
- **WHEN** the user changes precision on the frost alert entity to 0 in HA entity settings
- **THEN** HA displays -2°C instead of -2.4°C
