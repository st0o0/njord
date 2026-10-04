"""Diagnostics support for njord."""

from __future__ import annotations

from typing import Any

from homeassistant.config_entries import ConfigEntry
from homeassistant.core import HomeAssistant
from homeassistant.helpers.redact import async_redact_data

from .const import DOMAIN
from .coordinator import NjordDataCoordinator, NjordStatusCoordinator

REDACT_KEYS = {"host"}


async def async_get_config_entry_diagnostics(
    hass: HomeAssistant,
    entry: ConfigEntry,
) -> dict[str, Any]:
    data: dict[str, Any] = {}

    data["config"] = async_redact_data(dict(entry.data), REDACT_KEYS)
    data["options"] = dict(entry.options)

    entry_data = hass.data.get(DOMAIN, {}).get(entry.entry_id, {})

    coordinator: NjordDataCoordinator | None = entry_data.get("coordinator")
    if coordinator is not None and coordinator.data is not None:
        locations = sorted({loc for loc, _ in coordinator.data.forecasts})
        enrichment_types_per_location: dict[str, list[str]] = {}
        for loc in locations:
            enr = coordinator.data.enrichments.get(loc)
            if enr is not None:
                types = []
                if enr.alerts:
                    types.append("alerts")
                if enr.indices is not None:
                    types.append("indices")
                if enr.trends is not None:
                    types.append("trends")
                if enr.derived is not None:
                    types.append("derived")
                if enr.history is not None:
                    types.append("history")
                if enr.consensus is not None:
                    types.append("consensus")
                enrichment_types_per_location[loc] = types

        data["coordinator"] = {
            "locations": locations,
            "forecast_keys": len(coordinator.data.forecasts),
            "enrichment_types": enrichment_types_per_location,
            "active_enrichments": sorted(coordinator.data.active_enrichments)
            if coordinator.data.active_enrichments
            else None,
            "last_update_success": coordinator.last_update_success,
        }
        data["stream_states"] = dict(coordinator.stream_states)
    else:
        data["coordinator"] = None
        data["stream_states"] = None

    status_coordinator: NjordStatusCoordinator | None = entry_data.get("status_coordinator")
    if status_coordinator is not None and status_coordinator.data is not None:
        status = status_coordinator.data
        data["server_status"] = {
            "version": status.version,
            "uptime_seconds": status.uptime_seconds,
            "budget": {
                "monthly_limit": status.budget.monthly_limit,
                "monthly_used": status.budget.monthly_used,
                "daily_limit": status.budget.daily_limit,
                "daily_used": status.budget.daily_used,
                "usage_percent": status.budget.usage_percent,
            }
            if status.budget
            else None,
            "targets_count": len(status.targets),
        }
    else:
        data["server_status"] = "unavailable"

    return data
