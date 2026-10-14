## REMOVED Requirements

### Requirement: History sensor validation
**Reason**: njord has no history feature any more (no `ForecastHistoryActor`, no `HistoryResult`, no history payload in the gRPC API, field 6 of `GetEnrichmentsResponse` is reserved), so no model-performance data can reach Home Assistant.
**Migration**: Remove the history steps from `e2e/E2E-TEST-PLAN.md`, `.claude/skills/e2e-test/SKILL.md` and `e2e/docker-compose.e2e.yml`.
