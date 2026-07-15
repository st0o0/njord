import type { ModelInfo, NjordConfig } from './types'

const MONTH_MINUTES = 30 * 24 * 60
const GUARD_FACTOR = 0.8
const FREE_TIER_MONTHLY = 300_000

export interface BudgetResult {
  totalModelsPerCycle: number
  cyclesPerMonth: number
  apiCallWeight: number
  projected: number
  limit: number
  guard: number
  percentUsed: number
  overBudget: boolean
  perModelDays: { modelId: string; effectiveDays: number; maxHours: number | null }[]
}

export function computeBudget(config: NjordConfig, models: ModelInfo[]): BudgetResult {
  const pollMinutes = parseTimeSpan(config.pollInterval)
  const cyclesPerMonth = pollMinutes > 0 ? MONTH_MINUTES / pollMinutes : 0

  const hourlyCount = computeHourlyCount(config.parameters.groups)
  const daysWeight = Math.ceil(config.forecastDays / 14)
  const apiCallWeight = Math.ceil(hourlyCount / 10) * daysWeight

  const modelMap = new Map(models.map(m => [m.id.toLowerCase(), m]))

  let totalModelsPerCycle = 0
  const perModelDays: BudgetResult['perModelDays'] = []

  for (const location of config.locations) {
    const effectiveModels = resolveModels(config.models, location.models)
    totalModelsPerCycle += effectiveModels.length

    for (const modelId of effectiveModels) {
      const info = modelMap.get(modelId.toLowerCase())
      const maxHours = info?.maxForecastHours ?? null
      const effectiveDays = maxHours != null
        ? Math.min(config.forecastDays, Math.ceil(maxHours / 24))
        : config.forecastDays
      perModelDays.push({ modelId, effectiveDays, maxHours })
    }
  }

  const limit = config.budgetOverride?.requestsPerMonth ?? FREE_TIER_MONTHLY
  const guard = Math.round(limit * GUARD_FACTOR)
  const projected = Math.round(totalModelsPerCycle * cyclesPerMonth * apiCallWeight)
  const percentUsed = limit > 0 ? (projected / limit) * 100 : 0

  return {
    totalModelsPerCycle,
    cyclesPerMonth: Math.round(cyclesPerMonth),
    apiCallWeight,
    projected,
    limit,
    guard,
    percentUsed,
    overBudget: projected > guard,
    perModelDays,
  }
}

export function checkCoverage(
  modelId: string,
  lat: number,
  lon: number,
  models: ModelInfo[],
): boolean {
  const info = models.find(m => m.id.toLowerCase() === modelId.toLowerCase())
  if (!info || !info.bounds) return true
  const b = info.bounds
  return lat >= b.minLat && lat <= b.maxLat && lon >= b.minLon && lon <= b.maxLon
}

function resolveModels(global: string[], perLocation: string[]): string[] {
  const seen = new Set<string>()
  const result: string[] = []
  for (const m of [...global, ...perLocation]) {
    const lower = m.toLowerCase()
    if (!seen.has(lower)) {
      seen.add(lower)
      result.push(m)
    }
  }
  return result
}

function computeHourlyCount(groups: string[]): number {
  const counts: Record<string, number> = { Weather: 31, Solar: 9, Soil: 11 }
  return groups.reduce((sum, g) => sum + (counts[g] ?? 0), 0)
}

function parseTimeSpan(ts: string): number {
  const parts = ts.split(':').map(Number)
  if (parts.length === 3) return parts[0] * 60 + parts[1] + parts[2] / 60
  return 60
}
