## REMOVED Requirements

### Requirement: ConsensusBuilder aggregates multi-model forecasts
**Reason**: Consensus computation is fully replaced by the Open-Meteo Ensemble Mean API, which provides statistically superior aggregation from 51 ensemble members.
**Migration**: Ensemble mean and standard deviation are fetched directly from the API as DomainForecast<WeatherPoint>.
