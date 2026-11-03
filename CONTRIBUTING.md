# Contributing

Thanks for your interest in njörðr!

## Development

njord is a .NET 10 solution using Akka.NET. All commands run from `src/`:

```powershell
dotnet build Njord.slnx
dotnet format Njord.slnx --verify-no-changes
```

Tests use xUnit v3 on Microsoft Testing Platform - run with `dotnet run`, not `dotnet test`:

```powershell
dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj
```

## Pull requests

- Branch from `main` and open a PR. CI runs on pull requests.
- Commit messages follow [Conventional Commits](https://www.conventionalcommits.org/)
  (`feat:`, `fix:`, `docs:`, `ci:`, `chore:`, ...); commitlint enforces this and
  release-please derives the version and changelog from them.
- Keep changes focused and add or update tests for any behavior change.
- Code must pass `dotnet format --verify-no-changes`.

## Reporting bugs / requesting features

Use the issue templates. For security issues see [`SECURITY.md`](SECURITY.md).

By contributing you agree to the [Code of Conduct](CODE_OF_CONDUCT.md).
