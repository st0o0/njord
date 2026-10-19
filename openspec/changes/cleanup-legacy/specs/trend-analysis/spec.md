## REMOVED Requirements

### Requirement: TrendAnalyzer computes temporal trends
**Reason**: Trend analysis (direction, stability, predictability decay) required cross-cycle state and added complexity for limited value. If trends are needed in the future, they can be reimplemented as a ProjectionGroup with IRequirePrevious.
**Migration**: No direct replacement. Trends may be reintroduced as an optional ProjectionGroup in a future change.
