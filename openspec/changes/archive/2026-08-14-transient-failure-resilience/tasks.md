# Tasks: Transient Failure Resilience

- [x] Add TransientFailureCount to ModelPollState — add field, const, update Initial/WithDataChange/WithTransientFailure (`src/Njord/Pipeline/ModelPollState.cs`)
- [x] Update SchedulerActor call sites — pass discoveryInterval to WithTransientFailure (`src/Njord/Pipeline/SchedulerActor.cs`)
- [x] Add tests for transient failure isolation — MissCount poisoning prevention, cycle preservation, throttle cap, recovery (`src/Njord.Tests/Pipeline/ModelPollStateSpec.cs`)
