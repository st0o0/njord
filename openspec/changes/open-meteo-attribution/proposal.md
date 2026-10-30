# Open-Meteo Attribution Compliance

## Problem

Open-Meteo's licence (https://open-meteo.com/en/licence) requires CC BY 4.0 attribution with
three concrete obligations:

1. **Credit + licence link** -- name the source and link to CC BY 4.0.
2. **Indicate changes** -- state that the data has been transformed.
3. **Link next to displayed data** -- wherever Open-Meteo data are shown, include
   `<a href="https://open-meteo.com/">Weather data by Open-Meteo.com</a>`.

The upstream data sources (DWD, ECMWF, NOAA, Met Norway, UK Met Office CC-BY-SA, etc.)
each carry their own licence; Open-Meteo aggregates them under CC BY 4.0.

### Current state

| Location | Credit | Licence link | Changes noted | Link next to data |
|----------|--------|-------------|---------------|-------------------|
| njord README.md | yes | yes (CC BY 4.0) | no | n/a (no data display) |
| VitePress footer | text only | no link | no | n/a |
| VitePress docs pages | mentions Open-Meteo | no | no | n/a |
| ha-njord README.md | none | none | no | no |
| ha-njord HA integration UI | none | none | no | **this is where data is displayed** |

## Proposal

### njord repo

1. **VitePress footer** (docs/.vitepress/config.ts) -- replace the plain text with linked
   attribution:
   `Weather data by <a href="https://open-meteo.com/">Open-Meteo.com</a>, licensed under
   <a href="https://creativecommons.org/licenses/by/4.0/">CC BY 4.0</a>.
   Data is enriched and transformed by njord.`

2. **README.md License section** -- add the "changes made" note and the upstream reference:
   ```
   [MIT](LICENSE). Weather data from [Open-Meteo](https://open-meteo.com/) is licensed
   under [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/). njord enriches and
   transforms the raw forecast data. Open-Meteo aggregates data from national weather
   services (DWD, ECMWF, NOAA, and others) under their respective licences.
   ```

3. **docs/attribution.md** -- new page listing the upstream data sources and their licences,
   linked from the sidebar. This is not strictly required by CC BY 4.0 but makes the
   provenance transparent.

### ha-njord repo (separate change, tracked here for completeness)

4. **README.md** -- add Open-Meteo attribution in the License section, same pattern as njord.

5. **Integration manifest / device info** -- add an attribution URL or note visible in HA.
   HA custom integrations do not have a standard attribution field, but the integration's
   "about" or device info page can show it. This needs investigation into what HA supports.

## Non-goals

- Changing how data is served or displayed.
- Adding attribution to the gRPC wire format.
- Tracking individual upstream model licences per API response.
- Citation (DOI) -- optional per Open-Meteo, not a compliance requirement.

## Scope

This change covers items 1-3 (njord repo). Items 4-5 (ha-njord) are a separate change in
that repo, noted here so they are not forgotten.

## Risk

None. Documentation-only change, no code impact, no budget impact.
