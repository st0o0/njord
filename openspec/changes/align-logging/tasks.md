## 1. Audit actors

- [ ] 1.1 Scan all actors for `_log` field pattern — fix any that don't use inline readonly init
- [ ] 1.2 List: PollWorker, PollManager, ForecastPipeline, OpenMeteoGateway, BudgetTracker, GrpcConsumer, MqttConnection, MqttStatePublisher

## 2. Fix string interpolation in log calls

- [ ] 2.1 Scan for `_log.*\(\$"` pattern across all production code
- [ ] 2.2 Replace with structured message templates
- [ ] 2.3 Scan for `Log.*\(\$"` in ILogger calls (services)

## 3. Audit PipeTo mappers

- [ ] 3.1 Scan all PipeTo calls for log statements in mapper lambdas
- [ ] 3.2 Move any logging to the result handler

## 4. Audit log levels

- [ ] 4.1 Check actors for appropriate levels (Debug for routine, Info for lifecycle, Warning for errors, Error for fatal)
- [ ] 4.2 Fix any misleveled log calls

## 5. Verify ActorCategoryLogger

- [ ] 5.1 Verify `ActorCategoryLogger` covers all new actors (ProjectionEngine is not an actor, so n/a)
- [ ] 5.2 Verify log output uses type name as category

## 6. Validation

- [ ] 6.1 `dotnet build Njord.slnx`
- [ ] 6.2 All test projects pass
- [ ] 6.3 `grep -rn '_log.*\(\$"' src/Njord*/` returns nothing in production code
