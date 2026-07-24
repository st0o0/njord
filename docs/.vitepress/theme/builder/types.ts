export interface ModelInfo {
  id: string
  tier: string
  region: string
  bounds: { minLat: number; maxLat: number; minLon: number; maxLon: number } | null
  maxForecastHours: number | null
}

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
  latitude: number
  longitude: number
  models: string[]
}

export interface DeployLimits {
  cpus: string
  memory: string
}

export interface NjordConfig {
  pollInterval: string
  forecastDays: number
  discoveryInterval: string
  horizons: number[]
  models: string[]
  locations: LocationConfig[]
  parameters: { groups: string[]; extra: string[]; exclude: string[] }
  mqtt: { enabled: boolean; host: string; port: number; username: string; password: string; discoveryPrefix: string; discoveryEnabled: boolean; baseTopic: string }
  enrichment: Record<string, Record<string, unknown>>
  budgetOverride: { requestsPerMonth: number; requestsPerMinute: number } | null
  persistence: { provider: string; connectionString: string }
  persistencePath: string
  deploy: { limits: DeployLimits }
}

export function defaultConfig(): NjordConfig {
  return {
    pollInterval: '01:00:00',
    forecastDays: 4,
    discoveryInterval: '00:20:00',
    horizons: [3, 6, 12, 24, 48, 72],
    models: [],
    locations: [],
    parameters: { groups: ['Weather'], extra: [], exclude: [] },
    mqtt: { enabled: false, host: '', port: 1883, username: '', password: '', discoveryPrefix: 'homeassistant', discoveryEnabled: true, baseTopic: 'njord' },
    enrichment: {},
    budgetOverride: null,
    persistence: { provider: 'Sqlite', connectionString: '' },
    persistencePath: 'data/njord-journal.db',
    deploy: { limits: { cpus: '', memory: '' } },
  }
}
