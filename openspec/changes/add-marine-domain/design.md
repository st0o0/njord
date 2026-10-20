## Context

Fourth data domain following the established pattern: typed client → typed response → mapping → gateway route → IDomainHandler.

## Goals / Non-Goals

**Goals:** Marine data flowing through the pipeline, SwimmingConditions enrichment.
**Non-Goals:** Advanced marine enrichments (sailing, surfing scores).

## Decisions

### D1: Opt-in by default
Marine is not useful for inland locations. Add to `EnabledDomains` only when explicitly configured, unlike Weather/AQ/Flood which are default-on.

### D2: Swimming conditions from wave height + water temp
Simple threshold: wave_height > 2m = Dangerous, > 1m = Moderate, else Safe. Water temp < 15°C adds a "cold" warning. Pure logic, ~30 lines.
