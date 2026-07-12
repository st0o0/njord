## ADDED Requirements

### Requirement: Parameter groups are configured
The system SHALL accept a `Parameters` options section with `Groups` (list of group names, default `["Weather"]`), `Extra` (list of individual variable API names to add, default empty), and `Exclude` (list of individual variable API names to remove, default empty). The resolved parameter set SHALL be computed at startup and remain fixed for the process lifetime.

#### Scenario: Default parameter configuration
- **WHEN** no `Parameters` section is configured
- **THEN** the effective configuration is `Groups: ["Weather"], Extra: [], Exclude: []`

#### Scenario: Unknown group name is rejected
- **WHEN** configuration specifies `Groups: ["InvalidGroup"]`
- **THEN** startup validation fails naming the unknown group

#### Scenario: Unknown variable in Extra is rejected
- **WHEN** configuration specifies `Extra: ["not_a_real_variable"]`
- **THEN** startup validation fails naming the unknown variable

## MODIFIED Requirements

### Requirement: Startup validation enforces the budget projection
The system SHALL project monthly usage as `locations × models × cycles-per-month × call-weight` where call-weight is `ceil(active-hourly-variable-count / 10)`, and SHALL refuse to start when the projection exceeds 80% of the resolved monthly request budget, reporting the projection, the weight, and the limit in the error.

#### Scenario: Default Weather group passes with weight 3
- **WHEN** 1 location, 8 models, 60-minute poll interval, and the Weather group (~30 hourly variables, weight 3) are configured with the default budget
- **THEN** the projection is ≈ 17,280 effective requests/month and startup proceeds

#### Scenario: All groups active still passes on default budget
- **WHEN** 1 location, 8 models, 60-minute poll interval, and all groups (~50 hourly variables, weight 5) are configured with the default budget
- **THEN** the projection is ≈ 28,800 effective requests/month (within 80% of 300k) and startup proceeds

#### Scenario: Over-budget with high weight is rejected
- **WHEN** 3 locations, 8 models, 30-minute poll interval, and all groups (weight 5) are configured with the default budget
- **THEN** the projection is ≈ 172,800 effective requests/month, exceeding 80% of 300k, and startup fails reporting the projection, weight 5, and the 240,000 guard
