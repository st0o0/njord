import { requiredDomains, type NjordConfig } from './types'

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

// Mirrors BudgetCalculator.Validate: one call per (required domain, location) and poll cycle,
// multiplied by the API call weight (ceil(variable count / 10)).
export function computeBudget(config: NjordConfig): BudgetResult {
  const pollMinutes = parseTimeSpan(config.pollInterval)
  const cyclesPerDay = pollMinutes > 0 ? MINUTES_PER_DAY / pollMinutes : 0
  const cyclesPerMonth = cyclesPerDay * DAYS_PER_MONTH

  const apiCallWeight = Math.max(1, Math.ceil(variableCount(config.weather.includeSolar) / 10))

  const domainCallsPerCycle = requiredDomains(config.enrichments).length * config.locations.length

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

export function variableCount(includeSolar: boolean): number {
  return includeSolar ? 36 : 31
}

function parseTimeSpan(ts: string): number {
  const parts = ts.split(':').map(Number)
  if (parts.length === 3) return parts[0] * 60 + parts[1] + parts[2] / 60
  return 60
}
