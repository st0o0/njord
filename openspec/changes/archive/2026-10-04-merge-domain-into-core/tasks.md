## 1. Move Analysis files to Core

- [x] 1.1 Moved 46 files from `Njord.Domain/Analysis/` to `Njord.Core/Analysis/`
- [x] 1.2 Updated namespace: `Njord.Domain.Analysis` → `Njord.Analysis`
- [x] 1.3 Updated all `using Njord.Domain.Analysis` across codebase

## 2. Move Options files to Core/Configuration

- [x] 2.1 Moved 5 of 6 Options files to `Njord.Core/Configuration/` (LocationOptions stays in Domain — Messages needs it)
- [x] 2.2 Removed empty Options directory

## 3. Move Analysis tests to Core.Tests

- [x] 3.1 Moved 14 spec files to `Njord.Core.Tests/Analysis/`
- [x] 3.2 Updated namespaces in test files
- [x] 3.3 Updated using statements in test files

## 4. Update project references

- [x] 4.1 Added Newtonsoft.Json to Core
- [x] 4.2 Core already referenced Domain — no change needed
- [x] 4.3 Messages keeps Domain reference (no cycle)
- [x] 4.4 InternalsVisibleTo handled

## 5. Update architecture tests

- [x] 5.1 Architecture tests pass with updated layer (Core → Domain is allowed)
- [x] 5.2 Domain independence test still valid (Domain has no Njord refs)
- [x] 5.3 Convention checks updated

## 6. Update documentation and solution structure

- [x] 6.1 Updated AGENTS.md solution structure (Domain reduced, Core gains Analysis)
- [x] 6.2 Updated AGENTS.md test counts (Domain 70, Core 336)
- [x] 6.3 Reorganized Njord.slnx with /Foundation/, /Domain/, /Tests/ folders (FunkArr pattern)

## 7. Validation

- [x] 7.1 Build: 26 projects, 0 errors, 0 warnings
- [x] 7.2 All 859 tests pass
- [x] 7.3 Slopwatch: 0 issues
- [x] 7.4 dotnet format: clean
