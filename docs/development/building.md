# Building & testing

All .NET commands run from `src/` (where `global.json` lives).

## Build

```bash
dotnet build Njord.slnx
dotnet format Njord.slnx --verify-no-changes
```

`dotnet format` is enforced by CI. Run it after editing `.cs` files.

## Tests

Tests are xUnit v3 on [Microsoft.Testing.Platform](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-platform-intro): use `dotnet run`, **not** `dotnet test`.

```bash
# Single project
dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj

# Filter by class
dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj -- -class "Njord.Core.Tests.SomeSpec"

# All test projects sequentially (parallel runs slow actor tests)
for p in Njord.*Tests; do
  [ "$p" = Njord.Tests.Shared ] && continue
  dotnet run --project "$p/$p.csproj" --no-build
done
```

After any change, run **all** test projects, not only the affected ones. Snapshot tests (Verify) catch breaks in other projects.

## Test conventions

- Class suffix `Tests`, `public sealed`, layout mirrors production 1:1
- Names are `Snake_case_sentences` describing behavior
- Actor tests derive from `Akka.Hosting.TestKit.TestKit` with probes registered in `ActorRegistry`
- State records are tested as plain xUnit facts (Apply, ProcessCommand, snapshot round trip)
- Waits: `ExpectMsg` / `AwaitAssert` / `AwaitCondition`; `ExpectNoMsg(200 ms)` for absence. No `Thread.Sleep`
- `FakeTimeProvider` or a fixed timestamp, never `DateTime.UtcNow`
- NSubstitute only for plain interfaces, never actors (use probes)
- `Assert.NotNull` before nullable access, `Assert.Single`, count before indexing

## Run the service

```bash
dotnet run --project Njord/Njord.csproj
```

Uses the Development environment (`appsettings.Development.json`). HTTP on port 8080, gRPC on 8081.

## Docs site

```bash
cd docs
pnpm install
pnpm dev
```

The docs site uses VitePress with LikeC4 diagrams. Changes to `docs/likec4/*.c4` files update the diagrams live.
