## REMOVED Requirements

### Requirement: IndexAnalyzer computes activity and environmental indices
**Reason**: Activity indices (BBQ, Laundry, Outdoor, etc.) are unique value but represent significant code and test surface for a niche feature. Decision at implementation time whether to migrate as a ProjectionGroup or remove entirely.
**Migration**: If migrated, indices become a ProjectionGroup<WeatherIndexInput> working on DomainForecast<WeatherPoint>. If removed, no replacement.
