"""Tests for enrichment-based entity filtering."""

from __future__ import annotations

from unittest.mock import AsyncMock

from homeassistant.core import HomeAssistant
from homeassistant.helpers import entity_registry as er

from custom_components.njord.models import BudgetStatusData, ServerStatusData
from tests.conftest import init_integration


async def _setup_with_active_enrichments(
    hass: HomeAssistant,
    mock_client: AsyncMock,
    mock_config_entry,
    active: list[str],
) -> None:
    mock_client.get_status = AsyncMock(
        return_value=ServerStatusData(
            version="1.2.3",
            uptime_seconds=3600,
            budget=BudgetStatusData(
                monthly_limit=20000,
                monthly_used=5000,
                daily_limit=700,
                daily_used=100,
                usage_percent=25.0,
            ),
            active_enrichments=active,
        )
    )
    await init_integration(hass, mock_config_entry)


async def test_no_alert_entities_when_alerts_disabled(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    await _setup_with_active_enrichments(hass, mock_client, mock_config_entry, ["consensus", "derived", "indices"])

    registry = er.async_get(hass)
    alert_entities = [e for e in registry.entities.values() if "alert" in e.entity_id]
    assert len(alert_entities) == 0


async def test_no_trend_entity_when_trends_disabled(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    await _setup_with_active_enrichments(hass, mock_client, mock_config_entry, ["consensus", "alerts", "indices"])

    registry = er.async_get(hass)
    trend_entities = [e for e in registry.entities.values() if "trend" in e.entity_id]
    assert len(trend_entities) == 0


async def test_no_derived_entities_when_derived_disabled(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    await _setup_with_active_enrichments(hass, mock_client, mock_config_entry, ["alerts", "indices"])

    registry = er.async_get(hass)
    derived_ids = [
        e.entity_id
        for e in registry.entities.values()
        if any(
            k in e.entity_id for k in ("sunshine", "diurnal", "beaufort", "wind_chill", "dewpoint_comfort", "inversion")
        )
        and "_alert" not in e.entity_id
    ]
    assert len(derived_ids) == 0


async def test_no_consensus_when_disabled(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    await _setup_with_active_enrichments(hass, mock_client, mock_config_entry, ["alerts", "indices"])

    registry = er.async_get(hass)
    consensus_entities = [e for e in registry.entities.values() if "consensus" in e.entity_id]
    assert len(consensus_entities) == 0


async def test_all_enrichments_active_creates_all_entities(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    await _setup_with_active_enrichments(
        hass,
        mock_client,
        mock_config_entry,
        ["consensus", "alerts", "derived", "trends", "indices", "history"],
    )

    registry = er.async_get(hass)
    sensor_entities = [e for e in registry.entities.values() if e.domain == "sensor"]
    assert len(sensor_entities) >= 25


async def test_empty_active_enrichments_creates_no_enrichment_entities(
    hass: HomeAssistant, mock_client, mock_config_entry
) -> None:
    await _setup_with_active_enrichments(hass, mock_client, mock_config_entry, [])

    registry = er.async_get(hass)
    enrichment_domains = {"sensor", "binary_sensor", "event"}
    enrichment_entities = [
        e
        for e in registry.entities.values()
        if e.domain in enrichment_domains
        and "server" not in e.entity_id
        and "usage" not in e.entity_id
        and "version" not in e.entity_id
        and "uptime" not in e.entity_id
    ]
    assert len(enrichment_entities) == 0
