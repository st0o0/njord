## MODIFIED Requirements

### Requirement: VpdInfo sub-message

`common.proto` SHALL define a `VpdInfo` message with fields: `double kpa`, `VpdCategory category`. `VpdCategory` SHALL be an enum with `VPD_CATEGORY_UNSPECIFIED = 0`, `LOW`, `OPTIMAL`, `HIGH` and `CRITICAL`.

#### Scenario: VpdInfo carries category and value

- **WHEN** VPD is 1.27 kPa (category high)
- **THEN** `VpdInfo` SHALL have `kpa = 1.27` and `category = HIGH`
