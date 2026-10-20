export const DOMAINS = ['Weather', 'AirQuality', 'Flood', 'Marine'] as const

export type DomainName = (typeof DOMAINS)[number]

export const DEFAULT_DOMAINS: DomainName[] = ['Weather', 'AirQuality', 'Flood']

export interface ParameterGroup {
  group: string
  hourlyCount: number
  dailyCount: number
  hourlyVariables: string[]
  dailyVariables: string[]
}

export interface EnrichmentFeature {
  name: string
  enabledByDefault: boolean
  options: { name: string; type: string; default: unknown }[]
}

export interface LocationConfig {
  name: string
  latitude?: number
  longitude?: number
}

export interface DeployLimits {
  cpus: string
  memory: string
}

export interface NjordConfig {
  pollInterval: string
  forecastDays: number
  discoveryInterval: string
  enabledDomains: DomainName[]
  locations: LocationConfig[]
  parameters: { groups: string[]; extra: string[]; exclude: string[] }
  sensors: { enabled: boolean; stalenessSeconds: number }
  mqtt: { enabled: boolean; host: string; port: number; username: string; password: string; discoveryPrefix: string; baseTopic: string }
  enrichment: Record<string, Record<string, unknown>>
  budgetOverride: { requestsPerMonth: number; requestsPerMinute: number } | null
  persistence: { provider: string; connectionString: string }
  persistencePath: string
  openMeteoBaseUrl: string
  deploy: { limits: DeployLimits }
}

// Mirrors the defaults of src/Njord/appsettings.json (scalars) and the option classes (lists);
// the builder omits every value that equals its default, so keep these in sync with the shipped file.
export function defaultConfig(): NjordConfig {
  return {
    pollInterval: '01:00:00',
    forecastDays: 4,
    discoveryInterval: '00:20:00',
    enabledDomains: [...DEFAULT_DOMAINS],
    locations: [],
    parameters: { groups: ['Weather'], extra: [], exclude: [] },
    sensors: { enabled: true, stalenessSeconds: 7200 },
    mqtt: { enabled: false, host: '', port: 1883, username: '', password: '', discoveryPrefix: 'homeassistant', baseTopic: 'njord' },
    enrichment: {},
    budgetOverride: null,
    persistence: { provider: 'Sqlite', connectionString: '' },
    persistencePath: 'data/njord.db',
    openMeteoBaseUrl: 'https://api.open-meteo.com/',
    deploy: { limits: { cpus: '', memory: '' } },
  }
}
