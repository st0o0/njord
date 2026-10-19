## 1. Generic gateway routing

- [x] 1.1 Replace per-domain FetchDone records with one `DomainFetchDone(DomainKind, IDomainForecast)`
- [x] 1.2 Unify the domain-specific PipeTo chains into one generic handler
- [x] 1.3 Keep domain-specific client call dispatch (URL differs per domain)
- [x] 1.4 Delete unused per-domain records
- [x] 1.5 Update gateway tests

## 2. Client HTTP deduplication

- [x] 2.1 Extract shared HTTP fetch + JSON parse into private helper
- [x] 2.2 Each public method: build URL → call helper → map result
- [x] 2.3 Update client tests

## 3. Config-driven domains

- [x] 3.1 Add `EnabledDomains` to `NjordOptions` (default: Weather, AirQuality, Flood)
- [x] 3.2 Add validation (at least Weather must be enabled)
- [x] 3.3 PollWorker reads enabled domains from IOptions at construction
- [x] 3.4 Update PollWorker tests
- [x] 3.5 Update compose example and docs

## 4. Validation

- [x] 4.1 `dotnet build Njord.slnx`
- [x] 4.2 All test projects pass
