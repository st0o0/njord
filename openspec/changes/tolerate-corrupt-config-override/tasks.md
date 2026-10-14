## 1. Shared override-file state (Njord.Core)

- [x] 1.1 Write failing `ConfigOverrideFileSpec` in `src/Njord.Core.Tests/Configuration/` (buffered failure is logged at Error once a logger is attached; later failures log immediately; last good content is remembered)
- [x] 1.2 Add `ConfigOverrideFile` in `src/Njord.Core/Configuration/ConfigOverrideFile.cs`

## 2. Writer: corrupt file, lenient read, durable atomic write (Njord.Core)

- [x] 2.1 Write failing specs in `src/Njord.Core.Tests/Configuration/ConfigWriterSpec.cs`: corrupt file is moved aside to `.corrupt-<UTC timestamp>` with the original bytes and an Error log; new file holds last good content + mutation; never-loaded corrupt file yields mutation only; hand-edited file with comments/trailing commas is updated; successful write leaves no temp file
- [x] 2.2 Change `src/Njord.Core/Configuration/ConfigWriter.cs` to take `ConfigOverrideFile` and `TimeProvider`, parse leniently, move a corrupt file aside, and flush the temp file to disk before the rename
- [x] 2.3 Update `src/Njord.Tests.Shared/ConfigHarness.cs` and `src/Njord/Configuration/CoreSetupContainer.cs` to the new constructor

## 3. Tolerant configuration source (host)

- [x] 3.1 Write failing `ResilientJsonConfigurationSourceSpec` in `src/Njord.Tests/Configuration/`: truncated, empty and non-object files and a locked file do not throw at build, bind the remaining layers and log an Error; a corrupt runtime reload keeps the last good values; a repaired file is applied again; a valid file with invalid values still fails startup validation
- [x] 3.2 Add `ResilientJsonConfigurationSource`/`ResilientJsonConfigurationProvider` and the `AddResilientJsonFile` extension in `src/Njord/Configuration/`
- [x] 3.3 Wire `src/Njord/Program.cs` (source + singleton) and attach the logger in `src/Njord/Configuration/NjordApplicationSetup.cs`

## 4. Docs and specs

- [x] 4.1 Document the runtime override file and its corrupt-file handling in `docs/configuration/index.md`
- [x] 4.2 Update the test totals in `AGENTS.md`

## 5. Validation

- [x] 5.1 `dotnet build Njord.slnx` from `src/` with 0 warnings and 0 errors
- [x] 5.2 Run `dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj` and `dotnet run --project Njord.Tests/Njord.Tests.csproj` three times each without failures
- [x] 5.3 Run the full test loop from `AGENTS.md`
- [x] 5.4 Manual check: start the service from `src/Njord` with a corrupt `data/appsettings.Override.json`, confirm it starts and logs the Error, stop it and remove the test file
