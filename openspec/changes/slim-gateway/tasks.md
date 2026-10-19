## 1. Generic gateway routing

- [ ] 1.1 Replace per-domain FetchDone records with one `DomainFetchDone(DomainKind, IDomainForecast)`
- [ ] 1.2 Unify the domain-specific PipeTo chains into one generic handler
- [ ] 1.3 Keep domain-specific client call dispatch (URL differs per domain)
- [ ] 1.4 Delete unused per-domain records
- [ ] 1.5 Update gateway tests

## 2. Client HTTP deduplication

- [ ] 2.1 Extract shared HTTP fetch + JSON parse into private helper
- [ ] 2.2 Each public method: build URL → call helper → map result
- [ ] 2.3 Update client tests

## 3. Config-driven domains

- [ ] 3.1 Add `EnabledDomains` to `NjordOptions` (default: Weather, AirQuality, Flood)
- [ ] 3.2 Add validation (at least Weather must be enabled)
- [ ] 3.3 PollWorker reads enabled domains from IOptions at construction
- [ ] 3.4 Update PollWorker tests
- [ ] 3.5 Update compose example and docs

## 4. Validation

- [ ] 4.1 `dotnet build Njord.slnx`
- [ ] 4.2 All test projects pass
