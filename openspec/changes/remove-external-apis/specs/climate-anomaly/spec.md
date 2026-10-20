## REMOVED Requirements

### Requirement: Climate baseline fetched and cached at startup
**Reason**: The Climate Change API call (about 657 call-equivalents per location) is dropped; `seasonal-outlook` owns climate comparison.
**Migration**: None. The service no longer contacts `climate-api.open-meteo.com`.

### Requirement: ClimateAnomalyHandler compares forecast vs baseline
**Reason**: The ClimateAnomaly enrichment is removed.
**Migration**: Remove `ClimateAnomaly` from the enrichment configuration; the setting is ignored.

### Requirement: Anomaly reports temperature and precipitation
**Reason**: The ClimateAnomalyComputed event no longer exists.
**Migration**: None. Consumers stop receiving lifestyle updates of this kind.
