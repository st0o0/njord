## REMOVED Requirements

### Requirement: Commands with invalid references are silently dropped
**Reason**: This requirement only applied to `RefreshModel` and `RefreshLocation`
commands, which are being removed (no producer exists). The SchedulerActor's
own poll logic validates locations/models inline.
**Migration**: Re-add validation if manual refresh commands are reintroduced.
