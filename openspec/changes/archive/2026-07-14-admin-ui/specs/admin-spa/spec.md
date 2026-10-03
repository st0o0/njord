# admin-spa Specification

## Purpose

A Vue 3 single-page application served as static files from the njord container, providing a config editor and stats dashboard with charts.

## ADDED Requirements

### Requirement: SPA is built as static files and served by ASP.NET
The Vue.js SPA SHALL be built during the Docker image build (separate Node stage) and the output SHALL be copied to `wwwroot/`. ASP.NET SHALL serve the SPA via `UseStaticFiles()` with a fallback to `index.html` for client-side routing. No Node.js runtime SHALL exist in the final container image.

#### Scenario: SPA served at root
- **WHEN** a browser requests `/`
- **THEN** the response is the Vue SPA's `index.html`

#### Scenario: Client-side route fallback
- **WHEN** a browser requests `/settings/locations`
- **THEN** the response is `index.html` (Vue Router handles the route client-side)

#### Scenario: API routes are not intercepted by SPA fallback
- **WHEN** a browser requests `/api/config`
- **THEN** the response comes from the API endpoint, not the SPA fallback

### Requirement: SPA has a config editor with section-based forms
The SPA SHALL provide a config editor organized by sections matching the `NjordOptions` structure: General (poll interval, forecast days, discovery interval), Locations (CRUD for location entries), Models (global model list), Horizons (horizon list), Parameters (group selection, extra/exclude), Enrichment (per-feature toggle and settings), Budget (override values), MQTT (display-only for restart-required fields), Persistence (display-only for restart-required fields).

#### Scenario: Editing poll interval
- **WHEN** the user changes the poll interval to 30 minutes and clicks save
- **THEN** the SPA sends `PUT /api/config/pollInterval` and shows the updated value

#### Scenario: Adding a location
- **WHEN** the user fills in name, latitude, longitude and optional models, then clicks add
- **THEN** the SPA sends `PUT /api/config/locations` with the updated locations array

#### Scenario: Restart-required fields shown as read-only
- **WHEN** the user views the MQTT section
- **THEN** the host, port, and credentials fields are displayed but not editable, with a note that changes require container restart

### Requirement: SPA shows validation errors from the API
The SPA SHALL display validation errors returned by `PUT /api/config/{section}` (HTTP 400) inline next to the affected fields. The error messages SHALL be human-readable.

#### Scenario: Budget exceeded error displayed
- **WHEN** the user adds locations that would exceed the budget guard
- **THEN** the API returns 400 and the SPA displays the budget projection error near the locations section

### Requirement: SPA shows override indicators
The SPA SHALL visually indicate which config values are overridden (differ from base config). It SHALL provide a per-field "reset to default" action and a global "reset all overrides" action.

#### Scenario: Override indicator shown
- **WHEN** the poll interval is overridden from 60 to 30 minutes
- **THEN** the poll interval field has a visual indicator (e.g., dot or badge) and a reset button

#### Scenario: Reset single override
- **WHEN** the user clicks "reset" on the poll interval field
- **THEN** the SPA sends `DELETE /api/config/overrides/Njord:PollInterval` and the field reverts

### Requirement: SPA has a stats dashboard with charts
The SPA SHALL provide a dashboard page with Chart.js charts showing:
- **API activity**: fetch total and failure rates over time (line chart)
- **Fetch latency**: P50/P95/P99 over time (line chart)
- **MQTT status**: connection state, publish rate, reconnect events (line chart + status indicator)
- **Budget usage**: projected vs actual vs limit (gauge or bar chart)
- **Per-model activity**: fetch count per model over time (stacked bar or multi-line chart)

#### Scenario: Dashboard loads with 1-hour default window
- **WHEN** the user navigates to the dashboard page
- **THEN** charts are populated with the last 1 hour of data from `GET /api/stats/timeseries`

#### Scenario: Window selector changes chart range
- **WHEN** the user selects "24h" from the window selector
- **THEN** all charts refresh with 24-hour time-series data

#### Scenario: Current stats shown as summary cards
- **WHEN** the dashboard loads
- **THEN** summary cards show current totals (fetches, failures, MQTT publishes, uptime) from `GET /api/stats/current`

### Requirement: SPA auto-refreshes dashboard data
The dashboard SHALL poll the stats API at a configurable interval (default 30 seconds) to keep charts and counters up to date. A visual indicator SHALL show when data was last refreshed.

#### Scenario: Auto-refresh updates charts
- **WHEN** 30 seconds elapse since the last fetch
- **THEN** the dashboard fetches new data and updates all charts

#### Scenario: Auto-refresh indicator
- **WHEN** data is refreshed
- **THEN** a "last updated: X seconds ago" indicator is updated

### Requirement: SPA has a health overview page
The SPA SHALL provide a health page showing per-component health status from `GET /api/health`: MQTT connection, pipeline status, per-model scheduler state (phase, miss count, next poll time), and enrichment feature status (enabled/disabled, last computation time).

#### Scenario: Healthy system displayed
- **WHEN** all components are healthy
- **THEN** all status indicators show green/healthy

#### Scenario: Degraded MQTT shown
- **WHEN** MQTT has been disconnected for 1 minute
- **THEN** the MQTT component shows degraded status with the disconnection duration

### Requirement: SPA uses Vue 3 Composition API with TypeScript
The SPA SHALL be built with Vue 3 using the Composition API (`<script setup>`) and TypeScript. It SHALL use Vue Router for client-side navigation and Pinia (or equivalent) for state management if needed.

#### Scenario: TypeScript compilation
- **WHEN** the Vue project is built with `npm run build`
- **THEN** TypeScript compilation succeeds with no errors

### Requirement: SPA is self-contained with no external CDN dependencies
All JavaScript, CSS, and font assets SHALL be bundled into the build output. The SPA SHALL NOT load any resources from external CDNs at runtime. Chart.js SHALL be a local npm dependency.

#### Scenario: Offline-capable
- **WHEN** the container has no internet access
- **THEN** the SPA loads and functions fully from local static files
