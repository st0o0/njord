## 1. Spec reconciliation

- [x] 1.1 Write MODIFIED deltas (full requirement text with all scenarios) for `domain-setup-containers` (HealthSetupContainer, BudgetSetupContainer), `prometheus-metrics` (budget values), `mqtt-actor-topology` and `mqtt-adapter-boundary` so no main spec names `NjordHealthMonitor` or `MqttHealth` after archive; `openspec validate health-checks-redesign` must pass

## 2. Options

- [x] 2.1 Add `HealthOptions` (`Njord:Health`), validator, defaults in `appsettings.json`, `ConfigReloadPolicy` entry (hot), registration through `AddNjordOptions`; default-options golden master updated and reviewed

## 3. Feature-owned state and checks

- [x] 3.1 Pipeline: `PollHealthState`, `PollManager` writes it, `PipelineHealthCheck` moved to `Njord.Pipeline` with a static `Evaluate`; specs for all statuses and the grace period with `FakeTimeProvider`
- [x] 3.2 Mqtt: `MqttConnectionState` replaces `MqttHealth`, `MqttConnection` writes it, `MqttConnectionHealthCheck` reads it and `Njord:Health:MqttUnhealthyAfter`; specs for connected, not yet connected, degraded and unhealthy
- [x] 3.3 Budget: `BudgetUsageState` written by `BudgetTracker` on recovery and after each persisted event; `BudgetHealthCheck` (`Evaluate` over usage and effective budget from `ConfigView`); specs including a hot budget change
- [x] 3.4 Gauges: `BudgetGaugeRegistration` reads `BudgetUsageState` and `ConfigView`; `BudgetGaugeRegistrationSpec` extended for restart recovery; metric names unchanged

## 4. New host checks

- [x] 4.1 `journal` check with an `IFileProbe` seam (SQLite directory writable, PostgreSQL opens within 3 s) and specs
- [x] 4.2 `data-directory` check (Degraded) and specs
- [x] 4.3 `HealthSetupContainer` registers journal, data-directory with explicit `failureStatus`; the other containers register their own checks; `/healthz` gets the 5-second request timeout; `NjordApplicationSetup` spec covers `/healthz` and `/alive` status codes

## 5. Remove the monitor

- [x] 5.1 Delete `NjordHealthMonitor` and the health records from Core, remove `ConfigManager` limit writes and the MQTT allow-list entry, update architecture specs
- [x] 5.2 Update docs (health page), `AGENTS.md` (decisions and architecture notes) and the e2e documentation if it names the monitor

## 6. Verification

- [x] 6.1 Run `dotnet format whitespace`, build `Njord.slnx`, run every test project, `openspec validate --all --no-interactive`, `scripts/check-openspec-root.sh`
