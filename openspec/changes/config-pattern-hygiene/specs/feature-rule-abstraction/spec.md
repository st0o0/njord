## MODIFIED Requirements

### Requirement: FeatureDescriptor without Name
`FeatureDescriptor` SHALL contain `OutputKind Output`, `FeatureCategory Category`, `DomainRequirements Requirements`, and `int PollDepth`. It SHALL NOT contain a `Name`, any string identifier, or a `DefaultEnabled` flag. The default enabled state is determined solely by the config class property initializer.

#### Scenario: Descriptor carries all metadata except identity and defaults
- **WHEN** a feature exposes its descriptor
- **THEN** the descriptor SHALL contain category, output kind, domain requirements and poll depth, but no name, wire key, or default-enabled flag
