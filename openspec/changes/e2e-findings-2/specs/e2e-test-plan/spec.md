## ADDED Requirements

### Requirement: Plan expectations match real service behaviour
`e2e/E2E-TEST-PLAN.md` SHALL describe the observable behaviour of the current service and Home Assistant integration, so a correct run produces no false failures.

#### Scenario: Forecast and alert checks
- **WHEN** a tester follows S4.4, S7.3 and S7.5
- **THEN** S4.4 calls the `weather.get_forecasts` service (type hourly, `?return_response`) and expects a non-empty list (daily is unsupported); S7.3 expects `severity` only on alerts in state on, with a value in {yellow, orange, red}; S7.5 uses `pm2_5_alert`, not `pm25_alert`; the alert count is 21 entities matching `*_alert`

#### Scenario: gRPC response shapes
- **WHEN** a tester follows S8 and S9
- **THEN** S8.1/S8.2 expect the oneof JSON names `weather` / `airQuality`; S8.5 allows `precipitation` to be omitted when 0; Marine returns NotFound for inland locations by design; S9 expects `events[]` where the feature is the oneof case (e.g. `fog`), alert severity at `<case>.alert.severity`, no `type_name`, and S9.4 checks that an event has a feature oneof case

#### Scenario: Ops, health and budget checks
- **WHEN** a tester follows S10, S11, S12 and S13.4
- **THEN** S10.1 uses `GetCatalog` on `njord.v2.WeatherService`; S10.3 expects poll state per location (target phase and cycle); S11.1 treats `server_monthly_usage` as a percentage; S12.1 expects `/healthz` plain text `Healthy` or `Degraded`; S13.4 expects the increment to equal the sum of call weights of the resolved domains (e.g. 11 for the e2e config)
