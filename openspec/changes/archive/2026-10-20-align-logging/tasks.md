## 1. Audit actors

- [x] 1.1 Scan all actors for `_log` field pattern — fix any that don't use inline readonly init
- [x] 1.2 List: PollWorker, PollManager, ForecastPipeline, OpenMeteoGateway, BudgetTracker, GrpcConsumer, MqttConnection, MqttStatePublisher

## 2. Fix string interpolation in log calls

- [x] 2.1 Scan for `_log.*\(\$"` pattern across all production code
- [x] 2.2 Replace with structured message templates
- [x] 2.3 Scan for `Log.*\(\$"` in ILogger calls (services)

## 3. Audit PipeTo mappers

- [x] 3.1 Scan all PipeTo calls for log statements in mapper lambdas
- [x] 3.2 Move any logging to the result handler

## 4. Audit log levels

- [x] 4.1 Check actors for appropriate levels (Debug for routine, Info for lifecycle, Warning for errors, Error for fatal)
- [x] 4.2 Fix any misleveled log calls

## 5. Verify ActorCategoryLogger

- [x] 5.1 Verify `ActorCategoryLogger` covers all new actors (ProjectionEngine is not an actor, so n/a)
- [x] 5.2 Verify log output uses type name as category

## 6. Validation

- [x] 6.1 `dotnet build Njord.slnx`
- [x] 6.2 All test projects pass
- [x] 6.3 `grep -rn '_log.*\(\$"' src/Njord*/` returns nothing in production code
