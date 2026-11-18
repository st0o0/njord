## 1. Fix serialization

- [x] 1.1 In `PollWorkerPersistenceMappingExtensions.GetPersistenceState()`, replace `[.. state.Domains.Select(...)]` with `state.Domains.Select(...).ToList()` so the serialized collection type is `List<T>`

## 2. Close the test gap

- [x] 2.1 Add a roundtrip test in `PollWorkerMappingsTests` that builds a `PollWorkerState` with populated domains, calls `GetPersistenceState()`, and roundtrips via Newtonsoft with `TypeNameHandling.All`, asserting `DomainStates` survives
- [x] 2.2 Run all test projects and verify 0 failures
