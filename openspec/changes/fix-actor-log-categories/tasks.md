## 1. Failing specs first

- [ ] 1.1 Extract the logger setup of `src/Njord/Configuration/AkkaSetupContainer.cs` into `ConfigureLogging(AkkaConfigurationBuilder, IConfiguration)` (behavior unchanged) and call it from `BuildSystem`
- [ ] 1.2 Add `src/Njord.Tests/Logging/ActorLogCategorySpec.cs` (sealed, `[Fact(Timeout = TestTimeouts.Hosted)]`, BDD names): a generic host with `AddAkka` + `ConfigureLogging` and a capturing `ILoggerProvider`; an actor Info log arrives with the actor's full type name as category and keeps its named properties, `ActorPath` and `LogSource`; an Akka string-source log keeps an `Akka.` category; an actor Debug log arrives when `Serilog:MinimumLevel:Default` is `Debug`
- [ ] 1.3 Add a production-filter spec in the same file: Serilog built from the production `src/Njord/appsettings.json` (linked into the test output) behind a `SerilogLoggerFactory`; an actor Information log is captured with the actor type as `SourceContext`, an Akka Information log is not while an Akka Warning is; with an `"Njord.Tests": "Debug"` override an actor Debug log is captured
- [ ] 1.4 Add `src/Njord.Tests/Logging/AkkaLogLevelSpec.cs` (plain `[Fact]`/`[Theory]`): default/override/string-form/missing/unparseable minimum levels map to the expected Akka level
- [ ] 1.5 Run `src/Njord.Tests` and record the failures before the fix

## 2. Logger actor and Akka level

- [ ] 2.1 Add `src/Njord/Configuration/AkkaLogLevel.cs` (derive the Akka level from the `Serilog:MinimumLevel` section)
- [ ] 2.2 Add `src/Njord/Configuration/ActorCategoryLogger.cs` (sealed logger actor: category from `LogEvent.LogClass`, same structured properties as the Akka.Hosting bridge)
- [ ] 2.3 `ConfigureLogging` registers `ActorCategoryLogger` instead of `AddLoggerFactory()` and sets `LogLevel` from `AkkaLogLevel`
- [ ] 2.4 Run `src/Njord.Tests` green

## 3. Docs

- [ ] 3.1 `src/Njord/appsettings.Example.json`: stream-tracing comments name `Njord.Mqtt.MqttStatePublisher`; explain that actor categories are full type names and that the Akka level follows the lowest configured level
- [ ] 3.2 `docs/configuration/`: a short logging section (categories, overrides, Akka level)
- [ ] 3.3 `AGENTS.md`: update "Current total" and the per-project counts

## 4. Validation

- [ ] 4.1 From `src/`: `dotnet build Njord.slnx` with 0 warnings and 0 errors
- [ ] 4.2 From `src/`: `dotnet run --project Njord.Tests/Njord.Tests.csproj --no-build` and `dotnet run --project Njord.Architecture.Tests/Njord.Architecture.Tests.csproj --no-build`
- [ ] 4.3 From `src/`: the full test loop from `AGENTS.md`, all green, no `*.verified.*` changes
- [ ] 4.4 Manual: `cd src/Njord && timeout -s INT 40 dotnet run` shows njord actor lines with their class (e.g. `[Njord.Pipeline.PollScheduler]`); `src/Njord/data` restored afterwards
- [ ] 4.5 `openspec validate --all --no-interactive`
