import type { NjordConfig } from './types'

const MINUTES_PER_DAY = 24 * 60
const DAYS_PER_MONTH = 30
const GUARD_FACTOR = 0.8
const FREE_TIER_MONTHLY = 300_000

export interface BudgetResult {
  domainCallsPerCycle: number
  cyclesPerDay: number
  cyclesPerMonth: number
  apiCallWeight: number
  callsPerDay: number
  projected: number
  limit: number
  guard: number
  percentUsed: number
  overBudget: boolean
}

// Mirrors BudgetCalculator.Validate: one call per (enabled domain, location) and poll cycle,
// multiplied by the parameter weight of the resolved parameter set.
export function computeBudget(config: NjordConfig): BudgetResult {
  const pollMinutes = parseTimeSpan(config.pollInterval)
  const cyclesPerDay = pollMinutes > 0 ? MINUTES_PER_DAY / pollMinutes : 0
  const cyclesPerMonth = cyclesPerDay * DAYS_PER_MONTH

  const hourlyCount = computeHourlyCount(config.parameters.groups)
  const daysWeight = Math.ceil(config.forecastDays / 14)
  const apiCallWeight = Math.max(1, Math.ceil(hourlyCount / 10) * daysWeight)

  const domainCallsPerCycle = config.enabledDomains.length * config.locations.length

  const limit = config.budgetOverride?.requestsPerMonth ?? FREE_TIER_MONTHLY
  const guard = Math.round(limit * GUARD_FACTOR)
  const callsPerDay = Math.round(domainCallsPerCycle * cyclesPerDay * apiCallWeight)
  const projected = Math.round(domainCallsPerCycle * cyclesPerMonth * apiCallWeight)
  const percentUsed = limit > 0 ? (projected / limit) * 100 : 0

  return {
    domainCallsPerCycle,
    cyclesPerDay: Math.round(cyclesPerDay * 10) / 10,
    cyclesPerMonth: Math.round(cyclesPerMonth),
    apiCallWeight,
    callsPerDay,
    projected,
    limit,
    guard,
    percentUsed,
    overBudget: projected > guard,
  }
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
