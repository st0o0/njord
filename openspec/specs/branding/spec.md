# branding Specification

## Purpose

Visual identity for the njord project: logo SVG, brand color palette, and VitePress theme integration.

## Requirements

### Requirement: Logo SVG exists at docs/public/logo.svg
The project SHALL have an SVG logo at `docs/public/logo.svg` with a 48x48
viewBox. The logo SHALL be a geometric wind-rose/compass motif with 8
directional rays. It SHALL be monochrome and render correctly at 16px (favicon),
32px (navbar), and 120px (README hero) sizes.

#### Scenario: Logo renders as favicon
- **WHEN** the VitePress site is loaded in a browser
- **THEN** the browser tab shows the logo as a recognizable icon at 16x16px

#### Scenario: Logo works in light and dark mode
- **WHEN** the VitePress site is viewed in dark mode or light mode
- **THEN** the logo is visible and legible in both themes

### Requirement: Brand color palette replaces VitePress defaults
The VitePress theme SHALL override the default purple/violet brand colors with
a blue palette via CSS custom properties in a `custom.css` file:
`--vp-c-brand-1: #2563eb`, `--vp-c-brand-2: #3b82f6`,
`--vp-c-brand-3: #60a5fa`.

#### Scenario: Brand colors applied to interactive elements
- **WHEN** a user views the docs site
- **THEN** links, buttons, and the hero action button use the blue brand colors
  instead of the default VitePress purple

### Requirement: Logo appears in VitePress navbar
The VitePress config SHALL set `themeConfig.logo` to `/logo.svg` so the logo
appears in the navigation bar alongside the site title.

#### Scenario: Navbar shows logo
- **WHEN** any docs page is loaded
- **THEN** the navigation bar displays the logo SVG to the left of the "njord" title
