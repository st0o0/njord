## ADDED Requirements

### Requirement: GetManifest returns all entity definitions
The v3 NjordService SHALL expose a `GetManifest` RPC that returns an `EntityDefinition` for every entity the server can produce. The set SHALL be the cross-product of configured locations and enabled features, plus any server-wide entities (e.g. status sensors). Each `EntityDefinition` SHALL include `entity_id`, `location`, `feature` (the feature name), `platform` (weather, sensor, binary_sensor), `device_class`, `unit`, `icon`, and `category`.

#### Scenario: Manifest includes all enabled features for all locations
- **WHEN** njord is configured with 2 locations and 20 enabled features
- **THEN** `GetManifest` SHALL return at least 40 entity definitions (2 locations x 20 features) plus server-wide entities

#### Scenario: Manifest excludes disabled features
- **WHEN** a feature has `Enabled = false` in its config
- **THEN** `GetManifest` SHALL NOT include an entity definition for that feature

#### Scenario: Manifest includes secondary model weather entities
- **WHEN** njord is configured with 2 secondary Ensemble models and 1 location
- **THEN** `GetManifest` SHALL include 3 weather entity definitions for that location (1 primary + 2 secondary models)

### Requirement: Entity ID format is deterministic and stable
Each `entity_id` in the manifest SHALL be derived deterministically from the location name (slugified) and the feature name. The format SHALL be `{location_slug}_{feature_slug}` for feature entities and `{location_slug}_weather` for the primary weather entity. Secondary model weather entities SHALL use `{location_slug}_{model_slug}_weather`. IDs SHALL be stable across restarts given the same configuration.

#### Scenario: Entity ID for a feature
- **WHEN** location is "Vienna" and feature is "Frost"
- **THEN** `entity_id` SHALL be `vienna_frost`

#### Scenario: Entity ID for primary weather
- **WHEN** location is "Vienna"
- **THEN** the primary weather `entity_id` SHALL be `vienna_weather`

#### Scenario: Entity ID for secondary model weather
- **WHEN** location is "Vienna" and model is "ecmwf_ifs025_ensemble_mean"
- **THEN** `entity_id` SHALL be `vienna_ecmwf_ifs025_ensemble_mean_weather`

### Requirement: Manifest is static per process
The manifest SHALL be computed once at startup from the registered feature rules, configured locations, and active models. It SHALL NOT change during the lifetime of the process. A configuration change requires a process restart.

#### Scenario: Manifest is identical across multiple calls
- **WHEN** `GetManifest` is called twice without a restart
- **THEN** both responses SHALL be byte-identical

### Requirement: EntityMetadata comes from FeatureDescriptor
Each `EntityDefinition` SHALL derive its `platform`, `device_class`, `icon`, and `unit` from the `EntityMetadata` on the feature's `FeatureDescriptor`. The manifest builder SHALL NOT contain per-feature knowledge; it SHALL iterate registered rules and read their descriptors.

#### Scenario: Manifest reflects descriptor metadata
- **WHEN** a feature descriptor declares `EntityMetadata(Platform: Sensor, DeviceClass: "pm25", Unit: "ug/m3", Icon: "mdi:molecule")`
- **THEN** the corresponding `EntityDefinition` SHALL have those exact values
