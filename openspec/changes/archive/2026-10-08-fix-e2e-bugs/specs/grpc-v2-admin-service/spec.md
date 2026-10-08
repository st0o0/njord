## MODIFIED Requirements

### Requirement: SetBudget updates budget override
`AdminService.SetBudget` SHALL accept a `SetBudgetRequest` with optional `int32 requests_per_month` and `int32 requests_per_minute`. When both are omitted, it SHALL clear the budget override and revert to free-tier defaults. When `requests_per_month` is provided and is ≤ 0, the RPC SHALL return `ConfigResponse` with `applied = false` and a rejection reason. When `requests_per_minute` is provided and is ≤ 0, the RPC SHALL return `ConfigResponse` with `applied = false` and a rejection reason.

#### Scenario: Set custom budget
- **WHEN** a client sends `SetBudget` with `requests_per_month = 500000`
- **THEN** the budget override SHALL be applied and budget projection recalculated

#### Scenario: Clear budget override
- **WHEN** a client sends `SetBudget` with no fields set
- **THEN** the budget override SHALL be cleared, reverting to free-tier defaults

#### Scenario: Zero monthly budget rejected
- **WHEN** a client sends `SetBudget` with `requests_per_month = 0`
- **THEN** the RPC SHALL return `ConfigResponse` with `applied = false` and `rejection_reason` containing "must be greater than zero"

#### Scenario: Negative monthly budget rejected
- **WHEN** a client sends `SetBudget` with `requests_per_month = -100`
- **THEN** the RPC SHALL return `ConfigResponse` with `applied = false` and `rejection_reason` containing "must be greater than zero"

#### Scenario: Zero per-minute budget rejected
- **WHEN** a client sends `SetBudget` with `requests_per_minute = 0`
- **THEN** the RPC SHALL return `ConfigResponse` with `applied = false`

#### Scenario: Valid month with zero minute rejected
- **WHEN** a client sends `SetBudget` with `requests_per_month = 300000` and `requests_per_minute = 0`
- **THEN** the RPC SHALL return `ConfigResponse` with `applied = false`
