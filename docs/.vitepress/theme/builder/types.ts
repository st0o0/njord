export const DOMAINS = ['Weather', 'AirQuality', 'Flood', 'Marine'] as const

export type DomainName = (typeof DOMAINS)[number]

export interface EnrichmentToggle {
  key: string
  label: string
  description: string
  domains: DomainName[]
  weatherParams?: string[]
  optionalWeatherParams?: string[]
  airQualityParams?: string[]
  enabledByDefault: boolean
}

// Mirrors EnrichmentOptions (Njord:Enrichments) and the Requirements of each IDomainHandler; the domains
// that are fetched are the union of the domains the enabled handlers require.
export const ENRICHMENT_TOGGLES: EnrichmentToggle[] = [
  { key: 'WeatherAlerts', label: 'Weather alerts', description: 'Frost, heat, storm, rain and other weather alerts', domains: ['Weather'], weatherParams: ['Temperature', 'ApparentTemperature', 'WindSpeed', 'Precipitation', 'Humidity', 'DewPoint', 'Pressure', 'WindGusts', 'Visibility', 'Cape', 'Rain', 'Snowfall', 'FreezingLevelHeight', 'IsDay', 'EnsembleStdDev', 'UvIndex', 'SoilTemperature'], enabledByDefault: true },
  { key: 'CommuteSafety', label: 'Commute safety', description: 'Commute safety score', domains: ['Weather'], weatherParams: ['Temperature', 'DewPoint', 'Humidity', 'Precipitation', 'WindSpeed'], enabledByDefault: true },
  { key: 'PollenAlert', label: 'Pollen alert', description: 'Pollen concentration alerts', domains: ['AirQuality'], enabledByDefault: true },
  { key: 'Pm25Alert', label: 'PM2.5 alert', description: 'Fine particulate matter alerts', domains: ['AirQuality'], enabledByDefault: true },
  { key: 'FloodRisk', label: 'Flood risk', description: 'River discharge based flood risk', domains: ['Flood'], enabledByDefault: false },
  { key: 'FloodRain', label: 'Flood rain', description: 'Flood risk from rain and river discharge', domains: ['Flood', 'Weather'], weatherParams: ['Precipitation'], enabledByDefault: false },
  { key: 'SwimmingConditions', label: 'Swimming conditions', description: 'Waves and sea temperature', domains: ['Marine'], enabledByDefault: false },
  { key: 'VentilationWindow', label: 'Ventilation window', description: 'Best time to ventilate', domains: ['Weather', 'AirQuality'], weatherParams: ['Temperature', 'Precipitation'], enabledByDefault: false },
  { key: 'ExerciseAdvisory', label: 'Exercise advisory', description: 'Outdoor exercise advisory', domains: ['Weather', 'AirQuality'], weatherParams: ['Temperature', 'Humidity', 'UvIndex'], enabledByDefault: false },
  { key: 'LaundryScore', label: 'Laundry score', description: 'Drying conditions for laundry', domains: ['Weather'], weatherParams: ['Precipitation', 'Temperature', 'Humidity', 'WindSpeed', 'PrecipitationProbability', 'CloudCover', 'SunshineDuration'], enabledByDefault: false },
  { key: 'PollenTrend', label: 'Pollen trend', description: 'Pollen trend between forecasts', domains: ['AirQuality'], enabledByDefault: false },
  { key: 'NightVentilation', label: 'Night ventilation', description: 'Night cooling ventilation', domains: ['Weather'], weatherParams: ['Temperature'], enabledByDefault: false },
  { key: 'ConfidenceTrend', label: 'Confidence trend', description: 'Forecast confidence trend', domains: ['Weather'], weatherParams: ['EnsembleStdDev'], enabledByDefault: false },
  { key: 'SolarYield', label: 'Solar yield', description: 'Expected PV yield from tilted irradiance', domains: ['Weather'], weatherParams: ['GlobalTiltedIrradiance'], enabledByDefault: false },
  { key: 'GardeningAdvisory', label: 'Gardening advisory', description: 'Gardening conditions advisory', domains: ['Weather'], weatherParams: ['Temperature', 'WindSpeed', 'Precipitation'], enabledByDefault: false },
  { key: 'OutdoorScore', label: 'Outdoor score', description: 'Overall outdoor activity score', domains: ['Weather', 'AirQuality'], weatherParams: ['Temperature', 'Precipitation', 'PrecipitationProbability', 'WindSpeed', 'CloudCover'], optionalWeatherParams: ['UvIndex'], airQualityParams: ['BirchPollen', 'GrassPollen', 'AlderPollen', 'MugwortPollen'], enabledByDefault: false },
  { key: 'BBQScore', label: 'BBQ score', description: 'Barbecue conditions score', domains: ['Weather'], weatherParams: ['Precipitation', 'Temperature', 'WindSpeed', 'PrecipitationProbability', 'CloudCover'], enabledByDefault: false },
  { key: 'FloodTimeline', label: 'Flood timeline', description: 'River discharge timeline', domains: ['Flood'], enabledByDefault: false },
  { key: 'ApproachingFront', label: 'Approaching front', description: 'Weather front approaching detection', domains: ['Weather'], weatherParams: ['Pressure', 'Temperature', 'WindDirection'], enabledByDefault: false },
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

export function weatherVariableCount(enrichments: Record<string, boolean>): number {
  const params = new Set<string>()
  for (const toggle of ENRICHMENT_TOGGLES) {
    if (enrichments[toggle.key]) toggle.weatherParams?.forEach(p => params.add(p))
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
  enrichments: Record<string, boolean>
  locations: LocationConfig[]
  mqtt: { enabled: boolean; host: string; port: number; username: string; password: string; discoveryPrefix: string; baseTopic: string }
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
    enrichments: defaultEnrichments(),
    locations: [],
    mqtt: { enabled: false, host: '', port: 1883, username: '', password: '', discoveryPrefix: 'homeassistant', baseTopic: 'njord' },
    enrichment: {},
    budgetOverride: null,
    persistence: { provider: 'Sqlite', connectionString: '' },
    dataPath: 'data/njord.db',
    solarYield: { peakKw: 5.0, azimuth: 180, tilt: 30 },
    deploy: { limits: { cpus: '', memory: '' } },
  }
}
