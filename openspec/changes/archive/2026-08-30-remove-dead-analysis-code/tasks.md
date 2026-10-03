## 1. Remove dead methods

- [x] 1.1 Remove `IndexScorer.FrostProtection()` (lines 191-236) from `src/Njord/Domain/Analysis/IndexScorer.cs`
- [x] 1.2 Remove `IndexComputer.BuildEnvelope(List<int>)` (lines 356-366) from `src/Njord/Domain/Analysis/IndexComputer.cs`

## 2. Remove dead tests

- [x] 2.1 Remove `FrostProtection` test methods from `src/Njord.Tests/Domain/Analysis/IndexScorerSpec.cs`
- [x] 2.2 Remove `BuildEnvelope` test methods from `src/Njord.Tests/Domain/Analysis/IndexResultSpec.cs`

## 3. Validation

- [x] 3.1 Build: `dotnet build Njord.slnx` from `src/`
- [x] 3.2 Run affected test suites: `dotnet run --project Njord.Tests/Njord.Tests.csproj`
