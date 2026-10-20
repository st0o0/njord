## Context

Today a Weather field touches `WeatherParams`, the hourly DTO in `WeatherResponse.cs` (`[JsonPropertyName]`), `WeatherPoint` (ctor parameter, `Has` arm), `WeatherParamsMappings.ToApiName`, and `WeatherResponseMappings` (positional deconstruction of the DTO, `AnyAt` argument, constructor argument). Seasonal and Flood differ: Seasonal is monthly, caps the point count and ignores `PrecipitationMean` when deciding validity; Flood reads ensemble members from `JsonExtensionData`.

## Decisions

### D1. Static typed table per domain, no generator
Each domain gets one `static readonly FieldDefinition<TParams, TDto, TPoint>[] Fields`. Type safety comes from the lambdas: the DTO accessor is typed to the DTO, the setter to the Point, so a wrong property is a compile error. The only string is the API name. Rejected: a Roslyn incremental generator (extra netstandard2.0 project, build complexity, harder debugging, unnecessary at this size).

### D2. Where things live
- `FieldDefinition<,,>` and the tables live in `src/Njord.OpenMeteo/Mapping/`: they need the DTO types and API names are an OpenMeteo concern. `Njord.Forecast` must not reference other Njord projects (`ArchitectureSpec`).
- `HasFields` stays on the Point in `Njord.Forecast` (callers use `point.HasFields(required)`) and cannot read the OpenMeteo table. It uses a small Forecast-side presence list per Point (`flag -> Func<TPoint,bool>`), replacing the `Has()` switch with a data row per field. This is the one remaining parallel list; the completeness spec ties it to the table (D5).
- Open question for apply: if that duplicate is judged too costly, presence can move into the OpenMeteo table with `HasFields` becoming an extension there; callers in other projects would then need a reference change, so it is not the default.

### D3. FieldDefinition shape
`FieldDefinition<TParams, TDto, TPoint>(TParams Flag, string ApiName, Func<TDto, double?[]?> Series, Func<TPoint, double?, TPoint> Apply, FieldConversion Conversion = None)`. A typical row: `new(WeatherParams.Temperature, "temperature_2m", d => d.Temperature, (p, v) => p with { Temperature = v })`. For converted fields, factories `FieldDefinition.Int(...)` and `FieldDefinition.Bool(...)` take a setter typed `int?` / `bool?` and wrap the conversion (`(int)v`, `v > 0.5`), so the conversion is visible in the table and no object casts are needed.

### D4. Generic mapper
A shared `FieldMapper.ToDomainForecast` replaces the per-domain loops: guard on missing/empty timestamps; per index, `any = Fields.Any(f => At(f.Series(dto), i) is not null)` drives `lastValued`; start from the point factory and fold `Apply` over the fields; trim after `lastValued`; return null when nothing is valued; `ImmutableArray` result. Each domain mapper becomes a thin extension with the unchanged signature (`WeatherResponse.ToDomainForecast(modelRun)`), so callers and existing specs are untouched. `with` expressions allocate one record per field per point (Weather: ~32 x ~200 points per poll); negligible against the HTTP call and accepted. A mutable per-point accumulator is the fallback if a benchmark disagrees.

### D5. Completeness is a spec, not a convention
Per domain a spec asserts: every non-`None` enum member has exactly one table entry; API names are unique and equal the serialized name of the DTO property read by the entry (spec-only reflection over `[JsonPropertyName]`); every nullable Point property is written by exactly one entry (applying each setter to an empty point changes exactly one property); the Forecast-side presence list covers the same flags. Adding a field becomes: enum member, DTO property, Point property, one table row, one presence row (down from seven lists), with every mismatch caught by a test.

### D6. Per-domain scope
- Weather, AirQuality, Marine: full table, generic mapper, switches deleted.
- Flood: table drives `ToApiNames` and presence; the mapper stays hand-written for ensemble members.
- Seasonal: monthly, point-count cap, validity ignoring `PrecipitationMean`. Table drives names and presence. The mapper moves to the generic loop only if parity specs stay green with two optional additions (`maxPoints`, a per-field `CountsAsValue` flag); otherwise it stays hand-written. Decided in task 4.4.

### D7. Parity before deletion
Before deleting old code, a spec feeds identical DTOs through old and new paths (all valued, all null, trailing nulls, ragged series, missing series, code and bool conversions) and compares results. Existing specs must pass unchanged.

### D8. Request builder
Encode keys and each value with `Uri.EscapeDataString`, join list values with a literal `,` (URLs stay readable and identical for current inputs). `QueryHelpers.AddQueryString` is rejected: it would pull `Microsoft.AspNetCore.WebUtilities` into a library without an ASP.NET reference, and it encodes commas.

## Risks

- Behavior drift in trimming or conversions: mitigated by D7.
- Table, enum and presence list drifting apart: mitigated by D5 specs.
