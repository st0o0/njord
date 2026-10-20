export const DOMAINS = ['Weather', 'AirQuality', 'Flood', 'Marine'] as const

export type DomainName = (typeof DOMAINS)[number]

export interface EnrichmentToggle {
  key: string
  label: string
  description: string
  domains: DomainName[]
  enabledByDefault: boolean
}

// Mirrors EnrichmentOptions (Njord:Enrichments) and the Triggers of each IDomainHandler; the domains
// that are fetched are the union of the Triggers of the enabled toggles.
export const ENRICHMENT_TOGGLES: EnrichmentToggle[] = [
  { key: 'WeatherAlerts', label: 'Weather alerts', description: 'Frost, heat, storm, rain and other weather alerts', domains: ['Weather'], enabledByDefault: true },
  { key: 'CommuteSafety', label: 'Commute safety', description: 'Commute safety score', domains: ['Weather'], enabledByDefault: true },
  { key: 'PollenAlert', label: 'Pollen alert', description: 'Pollen concentration alerts', domains: ['AirQuality'], enabledByDefault: true },
  { key: 'Pm25Alert', label: 'PM2.5 alert', description: 'Fine particulate matter alerts', domains: ['AirQuality'], enabledByDefault: true },
  { key: 'FloodRisk', label: 'Flood risk', description: 'River discharge based flood risk', domains: ['Flood'], enabledByDefault: false },
  { key: 'FloodRain', label: 'Flood rain', description: 'Flood risk from rain and river discharge', domains: ['Flood', 'Weather'], enabledByDefault: false },
  { key: 'SwimmingConditions', label: 'Swimming conditions', description: 'Waves and sea temperature', domains: ['Marine'], enabledByDefault: false },
  { key: 'VentilationWindow', label: 'Ventilation window', description: 'Best time to ventilate', domains: ['Weather', 'AirQuality'], enabledByDefault: false },
  { key: 'ExerciseAdvisory', label: 'Exercise advisory', description: 'Outdoor exercise advisory', domains: ['Weather', 'AirQuality'], enabledByDefault: false },
  { key: 'LaundryScore', label: 'Laundry score', description: 'Drying conditions for laundry', domains: ['Weather'], enabledByDefault: false },
  { key: 'PollenTrend', label: 'Pollen trend', description: 'Pollen trend between forecasts', domains: ['AirQuality'], enabledByDefault: false },
  { key: 'NightVentilation', label: 'Night ventilation', description: 'Night cooling ventilation', domains: ['Weather'], enabledByDefault: false },
  { key: 'ConfidenceTrend', label: 'Confidence trend', description: 'Forecast confidence trend', domains: ['Weather'], enabledByDefault: false },
]

export function defaultEnrichments(): Record<string, boolean> {
  return Object.fromEntries(ENRICHMENT_TOGGLES.map(t => [t.key, t.enabledByDefault]))
}

export function requiredDomains(enrichments: Record<string, boolean>): DomainName[] {
  const result = new Set<DomainName>()
  for (const toggle of ENRICHMENT_TOGGLES) {
    if (enrichments[toggle.key]) toggle.domains.forEach(d => result.add(d))
  }
  return DOMAINS.filter(d => result.has(d))
}

export interface EnrichmentFeature {
  name: string
  enabledByDefault: boolean
  options: { name: string; type: string; default: unknown }[]
}

export interface LocationConfig {
  name: string
  latitude: number
  longitude: number
}

export interface DeployLimits {
  cpus: string
  memory: string
}

export interface NjordConfig {
  pollInterval: string
  weather: { includeSolar: boolean; forecastDays: number }
  discoveryInterval: string
  enrichments: Record<string, boolean>
  locations: LocationConfig[]
  mqtt: { enabled: boolean; host: string; port: number; username: string; password: string; discoveryPrefix: string; baseTopic: string }
  enrichment: Record<string, Record<string, unknown>>
  budgetOverride: { requestsPerMonth: number; requestsPerMinute: number } | null
  persistence: { provider: string; connectionString: string }
  dataPath: string
  openMeteoBaseUrl: string
  deploy: { limits: DeployLimits }
}

// Mirrors the defaults of src/Njord/appsettings.json (scalars) and the option classes (lists);
// the builder omits every value that equals its default, so keep these in sync with the shipped file.
export function defaultConfig(): NjordConfig {
  return {
    pollInterval: '01:00:00',
    weather: { includeSolar: false, forecastDays: 4 },
    discoveryInterval: '00:20:00',
    enrichments: defaultEnrichments(),
    locations: [],
    mqtt: { enabled: false, host: '', port: 1883, username: '', password: '', discoveryPrefix: 'homeassistant', baseTopic: 'njord' },
    enrichment: {},
    budgetOverride: null,
    persistence: { provider: 'Sqlite', connectionString: '' },
    dataPath: 'data/njord.db',
    openMeteoBaseUrl: 'https://api.open-meteo.com/',
    deploy: { limits: { cpus: '', memory: '' } },
  }
}
