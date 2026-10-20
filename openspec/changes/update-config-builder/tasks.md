## 1. Update types

- [ ] 1.1 Update types.ts: replace Models with EnabledDomains, remove Horizons

## 2. Update Vue component

- [ ] 2.1 Replace model selector with domain checkboxes in ConfigBuilder.vue
- [ ] 2.2 Remove horizons configuration section
- [ ] 2.3 Update location input to name-only with optional coordinate override
- [ ] 2.4 Update preview/summary section

## 3. Update budget calculation

- [ ] 3.1 Update budget.ts: domains × locations × cycles formula

## 4. Update serializer

- [ ] 4.1 Update serializer.ts: export EnabledDomains instead of Models
- [ ] 4.2 Export location names without coordinates when not explicitly set

## 5. Validation

- [ ] 5.1 VitePress site builds without errors
