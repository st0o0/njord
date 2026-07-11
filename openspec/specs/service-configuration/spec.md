# service-configuration Specification

## Purpose

Configuration and startup validation for the service: Kachelmann plan presets and budget overrides, monthly budget projection guards, environment-only API key handling, and minimal viable configuration defaults.

## Requirements

### Requirement: Plan presets resolve to request budgets
The system SHALL support the Kachelmann plans `Hobby`, `BusinessStarter`,
`BusinessStandard`, `BusinessProfessional`, `BusinessEnterprise`, and `Custom`
as configuration values, each resolving to a request budget (requests/month and
requests/minute). The `Hobby` preset SHALL resolve to 20,000 requests/month and
60 requests/minute. All throttling and validation SHALL consume the resolved
budget, never the plan name.

#### Scenario: Hobby preset resolves verified limits
- **WHEN** the configuration specifies plan `Hobby` and no budget override
- **THEN** the resolved budget is 20,000 requests/month and 60 requests/minute

#### Scenario: Custom plan requires an explicit budget
- **WHEN** the configuration specifies plan `Custom` without a budget override
- **THEN** startup validation fails with a message naming the missing budget

### Requirement: Budget override supersedes the preset
The system SHALL accept an optional budget override (requests/month,
requests/minute) that replaces the plan preset's values entirely.

#### Scenario: Override wins over preset
- **WHEN** plan `Hobby` is configured together with an override of 50,000
  requests/month and 120 requests/minute
- **THEN** the resolved budget is 50,000 requests/month and 120 requests/minute

### Requirement: Startup validation enforces the budget projection
The system SHALL project monthly usage as
`locations × models × cycles-per-month` (cycles derived from the poll
interval) and SHALL refuse to start when the projection exceeds 80 % of the
resolved monthly request budget, reporting the projection and the limit in the
error.

#### Scenario: Default configuration passes
- **WHEN** 1 location, 4 models, and a 60-minute poll interval are configured
  on plan `Hobby`
- **THEN** the projection is ≈ 2,880 requests/month and startup proceeds

#### Scenario: Over-budget configuration is rejected
- **WHEN** 6 locations, 4 models, and a 60-minute poll interval are configured
  on plan `Hobby`
- **THEN** startup fails, reporting a projection of ≈ 17,280 against the
  16,000 (80 %) guard

### Requirement: API key comes from the environment only
The system SHALL read the Kachelmann API key exclusively from configuration
bound to the environment variable `Njord__ApiKey` and SHALL fail startup with a
clear message when it is absent. The key MUST NOT appear in the repository, in
test fixtures, in logs, or in error messages.

#### Scenario: Missing API key blocks startup
- **WHEN** the service starts without `Njord__ApiKey` set
- **THEN** startup fails with a message naming the missing API key
  configuration

### Requirement: Minimal viable configuration is enforced
The system SHALL require at least one location (name, latitude, longitude) and
at least one non-empty model id, and SHALL default the poll interval to
60 minutes when unspecified.

#### Scenario: Empty model list is rejected
- **WHEN** the configuration contains a location but no models
- **THEN** startup validation fails naming the empty model list

#### Scenario: Poll interval defaults
- **WHEN** no poll interval is configured
- **THEN** the effective poll interval is 60 minutes
