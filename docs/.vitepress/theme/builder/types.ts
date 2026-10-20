import featureData from '../../../data/features.json'

export const DOMAINS = ['Weather', 'AirQuality', 'Flood', 'Marine', 'Seasonal', 'Satellite'] as const

export type DomainName = (typeof DOMAINS)[number]

export const CATEGORIES = [
  { key: 'Weather', label: 'Weather alerts' },
  { key: 'AirQuality', label: 'Air quality' },
  { key: 'Lifestyle', label: 'Lifestyle' },
  { key: 'Insight', label: 'Insight' },
  { key: 'Energy', label: 'Solar' },
  { key: 'Flood', label: 'Flood' },
  { key: 'Marine', label: 'Marine' },
] as const

export interface FeatureEntry {
  // Config key Category:Name; the wire key is the lowercase name.
  key: string
  name: string
  category: string
  output: 'Alert' | 'Score' | 'Condition' | 'Trend' | 'Forecast'
  description: string
  enabledByDefault: boolean
  domains: DomainName[]
  weatherParams?: string[]
  optionalWeatherParams?: string[]
  airQualityParams?: string[]
}

// Generated from the registered FeatureDescriptors (docs/data/features.json, kept in sync by FeaturesDataSpec),
// so the builder cannot drift from the service. Config keys are Njord:Features:<category>:<name>.
export const FEATURES: FeatureEntry[] = featureData as FeatureEntry[]

export function featureLabel(key: string): string {
  return key.slice(key.indexOf(':') + 1).replace(/([a-z0-9])([A-Z])/g, '$1 $2').replace('Pm25', 'PM2.5')
}

export function defaultFeatures(): Record<string, boolean> {
  return Object.fromEntries(FEATURES.map(f => [f.key, f.enabledByDefault]))
}

export function requiredDomains(features: Record<string, boolean>): DomainName[] {
  const result = new Set<DomainName>()
  for (const f of FEATURES) {
    if (features[f.key]) f.domains.forEach(d => result.add(d))
  }
  return DOMAINS.filter(d => result.has(d))
}

export function weatherVariableCount(features: Record<string, boolean>): number {
  const params = new Set<string>()
  for (const f of FEATURES) {
    if (!features[f.key]) continue
    f.weatherParams?.forEach(p => params.add(p))
    f.optionalWeatherParams?.forEach(p => params.add(p))
  }
  return params.size
}

export interface EnrichmentFeature {
  name: string
  enabledByDefault: boolean
  options: { name: string; type: string; default: unknown; description?: string }[]
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
  weather: { forecastDays: number; model: string }
  airQuality: { forecastDays: number }
  marine: { forecastDays: number }
  flood: { forecastDays: number; includeEnsemble: boolean }
  discoveryInterval: string
  features: Record<string, boolean>
  locations: LocationConfig[]
  enrichment: Record<string, Record<string, unknown>>
  budgetOverride: { requestsPerMonth: number; requestsPerMinute: number } | null
  persistence: { provider: string; connectionString: string }
  dataPath: string
  solarYield: { peakKw: number; azimuth: number; tilt: number }
  deploy: { limits: DeployLimits }
}

// Mirrors the defaults of src/Njord/appsettings.json (scalars) and the option classes (lists);
// the builder omits every value that equals its default, so keep these in sync with the shipped file.
export function defaultConfig(): NjordConfig {
  return {
    pollInterval: '01:00:00',
    weather: { forecastDays: 4, model: 'ecmwf_ifs025_ensemble_mean' },
    airQuality: { forecastDays: 5 },
    marine: { forecastDays: 8 },
    flood: { forecastDays: 92, includeEnsemble: false },
    discoveryInterval: '00:20:00',
    features: defaultFeatures(),
    locations: [],
    enrichment: {},
    budgetOverride: null,
    persistence: { provider: 'Sqlite', connectionString: '' },
    dataPath: 'data/njord.db',
    solarYield: { peakKw: 5.0, azimuth: 180, tilt: 30 },
    deploy: { limits: { cpus: '', memory: '' } },
  }
}
