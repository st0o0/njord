## REMOVED Requirements

### Requirement: RetryBackoffMax is configurable
**Reason**: `NjordOptions.RetryBackoffMax` exists as a config property (default 15 min) but is never read by any code. `ModelPollState` hard-codes the same value as a private constant. The unused config property creates confusion about whether backoff is configurable.
**Migration**: No migration needed — no code reads this property. The hard-coded constant in `ModelPollState` remains unchanged.
