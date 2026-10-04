## Purpose

Replaces the single combined API budget sensor with two dedicated usage percentage sensors (monthly and daily), each exposing limit and used values as attributes.

## Requirements

### Requirement: Monthly usage sensor shows percentage with limit and used
A `sensor.server_monthly_usage` entity SHALL display `monthly_used / monthly_limit * 100` as its state (unit: %). Attributes SHALL include `limit` and `used`.

#### Scenario: Normal monthly usage
- **WHEN** `BudgetStatus` has `monthly_limit=20000` and `monthly_used=5000`
- **THEN** the sensor's `native_value` is `25.0`
- **AND** attributes contain `limit=20000` and `used=5000`

#### Scenario: Monthly limit is zero
- **WHEN** `BudgetStatus` has `monthly_limit=0`
- **THEN** the sensor is unavailable

### Requirement: Daily usage sensor shows percentage with limit and used
A `sensor.server_daily_usage` entity SHALL display `daily_used / daily_limit * 100` as its state (unit: %). Attributes SHALL include `limit` and `used`.

#### Scenario: Normal daily usage
- **WHEN** `BudgetStatus` has `daily_limit=700` and `daily_used=100`
- **THEN** the sensor's `native_value` is `14.3`
- **AND** attributes contain `limit=700` and `used=100`

#### Scenario: Daily limit is zero
- **WHEN** `BudgetStatus` has `daily_limit=0`
- **THEN** the sensor is unavailable

### Requirement: Usage sensors are diagnostic on the Server device
Both usage sensors SHALL have `entity_category=DIAGNOSTIC`, belong to the Server device, and use `NjordStatusCoordinator`.

#### Scenario: Sensor metadata
- **WHEN** the integration is set up
- **THEN** both sensors are registered as diagnostic entities on the Server device

### Requirement: Old combined API budget sensor is removed
`NjordApiBudgetSensor` (`sensor.server_api_budget`) SHALL be removed.

#### Scenario: Entity does not exist
- **WHEN** the integration is set up
- **THEN** no `sensor.server_api_budget` entity is created
