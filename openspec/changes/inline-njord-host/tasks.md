## 1. Inline NjordHost into call sites

- [x] 1.1 Inline the setup chain from `NjordHost.Configure(builder)` and `NjordHost.Configure(app)` into `src/Njord/Program.cs`. The file should create the builder, call `ClearProviders()`, chain all 12 `.Setup(new XxxSetup())` calls, build, call `app.Setup(new NjordApplicationSetup())`, then `RunAsync()`.
- [x] 1.2 Inline the same setup chain into `src/Njord.IntegrationTests/NjordTestHost.cs`, replacing the two `NjordHost.Configure` calls. The service overrides and `UseTestServer()` stay between the configure and build steps. Update the class comment.
- [x] 1.3 Delete `src/Njord/NjordHost.cs`.
- [x] 1.4 Update `AGENTS.md`: remove `NjordHost` from the solution structure description, replace with `Program.cs (setup chain)`.

### Validation

```bash
cd src
dotnet build Njord.slnx
dotnet format Njord.slnx --verify-no-changes
for p in Njord.*Tests; do [ "$p" = Njord.Tests.Shared ] && continue; dotnet run --project "$p/$p.csproj" --no-build; done
```
