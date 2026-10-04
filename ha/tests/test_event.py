"""Tests for njord event entities."""

from __future__ import annotations

from dataclasses import replace

from homeassistant.core import HomeAssistant
from homeassistant.helpers import entity_registry as er

from custom_components.njord.const import DOMAIN
from custom_components.njord.coordinator import NjordCoordinatorData
from custom_components.njord.models import AlertData
from tests.conftest import init_integration


async def test_event_entity_exists(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    await init_integration(hass, mock_config_entry)

    registry = er.async_get(hass)
    entry = registry.async_get("event.home_weather_alert")
    assert entry is not None


async def test_event_entity_icon(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    await init_integration(hass, mock_config_entry)

    state = hass.states.get("event.home_weather_alert")
    assert state is not None
    assert state.attributes["icon"] == "mdi:weather-lightning-rainy"


async def test_no_events_on_initial_load(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    await init_integration(hass, mock_config_entry)

    state = hass.states.get("event.home_weather_alert")
    assert state is not None
    assert state.attributes.get("event_type") is None


def _update_alerts(coordinator, alerts: list[AlertData]) -> None:
    new_data = NjordCoordinatorData(
        forecasts=dict(coordinator.data.forecasts),
        enrichments={
            k: replace(v, alerts=alerts) if k == "home" else v for k, v in coordinator.data.enrichments.items()
        },
        model_info=dict(coordinator.data.model_info),
    )
    coordinator.async_set_updated_data(new_data)


async def test_alert_started_event(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    await init_integration(hass, mock_config_entry)

    state = hass.states.get("event.home_weather_alert")
    assert state is not None, "Event entity should exist after setup"
    assert state.attributes.get("event_type") is None, "No event should fire on initial load"

    coordinator = hass.data[DOMAIN][mock_config_entry.entry_id]["coordinator"]

    _update_alerts(
        coordinator,
        [
            AlertData(type="uv", severity="orange", confidence=1.0, trigger_value=8.5, threshold=6.0),
            AlertData(type="frost", severity="yellow", confidence=0.8, trigger_value=-1.2, threshold=0.0),
            AlertData(type="heat", severity="yellow", confidence=0.33, trigger_value=38.2, threshold=35.0),
        ],
    )
    await hass.async_block_till_done()

    state = hass.states.get("event.home_weather_alert")
    assert state is not None
    assert state.attributes.get("event_type") == "alert_started", f"attrs={dict(state.attributes)}"
    assert state.attributes.get("type") == "frost"
    assert state.attributes.get("location") == "home"


async def test_alert_cleared_event(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    await init_integration(hass, mock_config_entry)

    coordinator = hass.data[DOMAIN][mock_config_entry.entry_id]["coordinator"]

    _update_alerts(
        coordinator,
        [
            AlertData(type="frost", severity="none", confidence=0.0),
            AlertData(type="heat", severity="yellow", confidence=0.33, trigger_value=38.2, threshold=35.0),
        ],
    )
    await hass.async_block_till_done()

    state = hass.states.get("event.home_weather_alert")
    assert state is not None
    assert state.attributes.get("event_type") == "alert_cleared"
    assert state.attributes.get("type") == "uv"
    assert state.attributes.get("location") == "home"
    assert state.attributes.get("previous_severity") == "orange"


async def test_alert_escalated_event(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    await init_integration(hass, mock_config_entry)

    coordinator = hass.data[DOMAIN][mock_config_entry.entry_id]["coordinator"]

    _update_alerts(
        coordinator,
        [
            AlertData(type="uv", severity="red", confidence=1.0, trigger_value=10.0, threshold=6.0),
            AlertData(type="frost", severity="none", confidence=0.0),
            AlertData(type="heat", severity="yellow", confidence=0.33, trigger_value=38.2, threshold=35.0),
        ],
    )
    await hass.async_block_till_done()

    state = hass.states.get("event.home_weather_alert")
    assert state is not None
    assert state.attributes.get("event_type") == "alert_escalated"
    assert state.attributes.get("type") == "uv"
    assert state.attributes.get("location") == "home"
    assert state.attributes.get("previous_severity") == "orange"


async def test_alert_deescalated_event(hass: HomeAssistant, mock_client, mock_config_entry) -> None:
    await init_integration(hass, mock_config_entry)

    coordinator = hass.data[DOMAIN][mock_config_entry.entry_id]["coordinator"]

    _update_alerts(
        coordinator,
        [
            AlertData(type="uv", severity="yellow", confidence=0.5, trigger_value=5.0, threshold=6.0),
            AlertData(type="frost", severity="none", confidence=0.0),
            AlertData(type="heat", severity="yellow", confidence=0.33, trigger_value=38.2, threshold=35.0),
        ],
    )
    await hass.async_block_till_done()

    state = hass.states.get("event.home_weather_alert")
    assert state is not None
    assert state.attributes.get("event_type") == "alert_deescalated"
    assert state.attributes.get("type") == "uv"
    assert state.attributes.get("location") == "home"
    assert state.attributes.get("previous_severity") == "orange"
