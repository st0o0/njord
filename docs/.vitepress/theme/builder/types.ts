import featureData from '../../../data/features.json'

export const DOMAINS = ['Weather', 'Ensemble', 'AirQuality', 'Flood', 'Marine', 'Seasonal', 'Satellite'] as const

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
  ensembleParams?: string[]
  options?: FeatureOption[]
}

// A per-feature setting below Njord:Features:<Category>:<Name>; null/absent in the config means the built-in default.
export interface FeatureOption {
  name: string
  type: 'number' | 'numberArray'
  default: number | number[]
}

export type OptionValue = number | number[]

// docs/data/features.json mirrors the registered FeatureRules (descriptor requirements plus the options of each
// feature config class) and is maintained by hand together with them (see the njord-enrichment-feature skill).
// Config keys are Njord:Features:<category>:<name>:Enabled and Njord:Features:<category>:<name>:<option>.
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
  // weather_code is always requested when the weather domain is required (base parameter)
  if (params.size > 0) params.add('WeatherCode')
  return params.size
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
  weather: { forecastDays: number; models: string[] }
  airQuality: { forecastDays: number; region: string }
  marine: { forecastDays: number }
  flood: { forecastDays: number; includeEnsemble: boolean }
  discoveryInterval: string
  features: Record<string, boolean>
  locations: LocationConfig[]
  // Only values that differ from the feature default: featureOptions['Weather:Frost'].Thresholds = [0, -5, -15]
  featureOptions: Record<string, Record<string, OptionValue>>
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
    weather: { forecastDays: 4, models: ['ecmwf_ifs025_ensemble_mean'] },
    airQuality: { forecastDays: 5, region: 'Global' },
    marine: { forecastDays: 8 },
    flood: { forecastDays: 92, includeEnsemble: false },
    discoveryInterval: '00:20:00',
    features: defaultFeatures(),
    locations: [],
    featureOptions: {},
    budgetOverride: null,
    persistence: { provider: 'Sqlite', connectionString: '' },
    dataPath: 'data/njord.db',
    solarYield: { peakKw: 5.0, azimuth: 180, tilt: 30 },
    deploy: { limits: { cpus: '', memory: '' } },
  }
}
